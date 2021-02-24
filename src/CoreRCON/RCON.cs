using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreRCON
{
    public partial class RCON : IDisposable
    {
        internal static string Identifier = "";

        // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
        private TaskCompletionSource<bool> _authenticationTask;

        private bool _connected = false;

        private IPEndPoint _endpoint;

        // When generating the packet ID, use a never-been-used (for automatic packets) ID.
        private int _packetId = 1;

        private string _password;
        private int _timeout;
        private bool _multiPacket;

        // Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
        private ConcurrentDictionary<int, TaskCompletionSource<string>> _pendingCommands { get; } = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        private Dictionary<int, string> _incomingBuffer { get; } = new Dictionary<int, string>();

        private Socket _tcp { get; set; }
        private Task _networkConsumerTask;

        public event Action OnDisconnected;

        /// <summary>
        /// Create RCON object, Se main constructor for more info
        /// </summary>
        /// <param name="host">Server address</param>
        /// <param name="port">Server port</param>
        public RCON(IPAddress host, ushort port, string password, uint tcpTimeout = 10000, bool sourceMultiPacketSupport = false)
            : this(new IPEndPoint(host, port), password, tcpTimeout, sourceMultiPacketSupport)
        { }

        /// <summary>
        /// Create RCON object 
        /// </summary>
        /// <param name="endpoint">Server to connect to</param>
        /// <param name="password">Rcon password</param>
        /// <param name="tcpTimeout">TCP socket send and receive timeout in milliseconds. A value of 0 means no timeout</param>
        /// <param name="sourceMultiPacketSupport">Enable source engine trick to receive multi packet responses using trick by Koraktor</param>
        public RCON(IPEndPoint endpoint, string password, uint tcpTimeout = 10000, bool sourceMultiPacketSupport = false)
        {
            _endpoint = endpoint;
            _password = password;
            _timeout = (int)tcpTimeout;
            _multiPacket = sourceMultiPacketSupport;
        }

        /// <summary>
        /// Connect to a server through RCON.  Automatically sends the authentication packet.
        /// </summary>
        /// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
        public async Task ConnectAsync()
        {
            if (_connected)
            {
                return;
            }
            _tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _tcp.ReceiveTimeout = _timeout;
            _tcp.SendTimeout = _timeout;
            _tcp.NoDelay = true;
            await _tcp.ConnectAsync(_endpoint);
            _connected = true;
            Pipe pipe = new Pipe();
            Task writing = FillPipeAsync(pipe.Writer);
            Task reading = ReadPipeAsync(pipe.Reader);

            // Wait for successful authentication
            _authenticationTask = new TaskCompletionSource<bool>();
            await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password));
            _networkConsumerTask = Task.WhenAll(writing, reading)
                .ContinueWith(t =>
                {
                    var aggException = t.Exception.Flatten();
                    Console.Error.WriteLine("RCON connection closed");
                    foreach (var exception in aggException.InnerExceptions)
                        Console.Error.WriteLine($"Exception {exception.Message}");
                },
                TaskContinuationOptions.OnlyOnFaulted);
            await _authenticationTask.Task;
        }

        /// <summary>
        /// Fill pipe with data when available in the socket
        /// </summary>
        /// <param name="writer"></param>
        /// <returns>Producer Task</returns>
        async Task FillPipeAsync(PipeWriter writer)
        {
            const int minimumBufferSize = Constants.MIN_PACKET_SIZE;

            try
            {
                while (_connected)
                {
                    // Allocate at least 14 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                    int bytesRead = await _tcp.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);

                    // Make the data available to the PipeReader
                    FlushResult result = await writer.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                // Tell the PipeReader that there's no more data coming
                await writer.FlushAsync();
                await writer.CompleteAsync();
                _connected = false;
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Read data from pipeline when available, constructing new RCON packets 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>Consumer Task</returns>
        async Task ReadPipeAsync(PipeReader reader)
        {
            byte[] byteArr = new byte[Constants.MAX_PACKET_SIZE];
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition packetStart = buffer.Start;

                if (buffer.Length < 4)
                {
                    if (result.IsCompleted)
                    {
                        break;
                    }
                    reader.AdvanceTo(packetStart, buffer.End);
                    continue;
                    // Complete header not yet received
                }
                int size = BitConverter.ToInt32(buffer.Slice(packetStart, 4).ToArray(), 0);
                if (buffer.Length >= size + 4)
                {
                    // Get packet end positions 
                    SequencePosition packetEnd = buffer.GetPosition(size + 4, packetStart);
                    byteArr = buffer.Slice(packetStart, packetEnd).ToArray();
                    RCONPacket packet = RCONPacket.FromBytes(byteArr);

                    if (packet.Type == PacketType.AuthResponse)
                    {
                        // Failed auth responses return with an ID of -1
                        if (packet.Id == -1)
                        {
                            _authenticationTask.SetException(
                                new AuthenticationException($"Authentication failed for {_tcp.RemoteEndPoint}.")
                                );
                        }
                        // Tell Connect that authentication succeeded
                        _authenticationTask.SetResult(true);
                    }

                    // Forward rcon packet to handler
                    RCONPacketReceived(packet);

                    reader.AdvanceTo(packetEnd);
                }
                else
                {
                    reader.AdvanceTo(packetStart, buffer.End);
                }

                // Tell the PipeReader how much of the buffer we have consumed

                // Stop reading if there's no more data coming
                if (buffer.IsEmpty && result.IsCompleted)
                {
                    break; // exit loop
                }
            }

            // If authentication did not complete
            _authenticationTask.TrySetException(
                                new AuthenticationException($"Server did not respond to auth {_tcp.RemoteEndPoint}.")
                                );

            // Mark the PipeReader as complete
            await reader.CompleteAsync();
        }

        public void Dispose()
        {
            _connected = false;
            _tcp.Shutdown(SocketShutdown.Both);
            _tcp.Dispose();
        }

        /// <summary>
        /// Send a command to the server, and wait for the response before proceeding.  Expect the result to be parsable into T.
        /// </summary>
        /// <typeparam name="T">Type to parse the command as.</typeparam>
        /// <param name="command">Command to send to the server.</param>
        /// <exception cref = "System.FormatException" > Unable to parse response </ exception >
        /// <exception cref = "System.AggregateException" >Connection exceptions</ exception >
        public async Task<T> SendCommandAsync<T>(string command)
            where T : class, IParseable, new()
        {
            string response = await SendCommandAsync(command);
            // Se comment about TaskCreationOptions.RunContinuationsAsynchronously in SendComandAsync<string>
            var source = new TaskCompletionSource<T>();
            var instance = ParserHelpers.CreateParser<T>();
            var container = new ParserContainer
            {
                IsMatch = line => instance.IsMatch(line),
                Parse = line => instance.Parse(line),
            };


            object parsed;
            if (!container.TryParse(response, out parsed))
            {
                throw new FormatException("Failed to parse server response");
            }
            return (T)parsed;
        }

        /// <summary>
        /// Send a command to the server, and wait for the response before proceeding.  R
        /// </summary>
        /// <param name="command">Command to send to the server.</param>
        /// <exception cref = "System.AggregateException" >Connection exceptions</ exception >
        public async Task<string> SendCommandAsync(string command)
        {

            // This TaskCompletion source could be initialized with TaskCreationOptions.RunContinuationsAsynchronously
            // However we this library is designed to be able to run without its own thread
            // Read more about this option here:
            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#always-create-taskcompletionsourcet-with-taskcreationoptionsruncontinuationsasynchronously
            var source = new TaskCompletionSource<string>();
            int packetId = Interlocked.Increment(ref _packetId);
            if(!_pendingCommands.TryAdd(packetId, source))
            {
                throw new SocketException();
            }
            RCONPacket packet = new RCONPacket(packetId, PacketType.ExecCommand, command);

            await SendPacketAsync(packet);
            await Task.WhenAny(source.Task, _networkConsumerTask);
            if (source.Task.IsCompleted)
            {
                return source.Task.Result;
            }

            if (_networkConsumerTask.IsFaulted)
            {
                throw _networkConsumerTask.Exception.InnerException;
            }
            else
            {
                throw new SocketException();
            }
        }

        /// <summary>
        /// Merges RCON packet bodies and resolves the waiting task
        /// with the full body when full response has been recived. 
        /// </summary>
        /// <param name="packet"> Newly received packet </param>
        private void RCONPacketReceived(RCONPacket packet)
        {
            // Call pending result and remove from map
            if (_pendingCommands.TryGetValue(packet.Id, out TaskCompletionSource<string> taskSource))
            {
                if (_multiPacket)
                {
                    //Read any previous messages 
                    string body;
                    _incomingBuffer.TryGetValue(packet.Id, out body);

                    if (packet.Body == "")
                    {
                        //Avoid yielding
                        taskSource.SetResult(body ?? string.Empty);
                        _pendingCommands.TryRemove(packet.Id, out _);
                        _incomingBuffer.Remove(packet.Id);
                    }
                    else
                    {
                        //Append to previous messages
                        _incomingBuffer[packet.Id] = body + packet.Body;
                    }
                }
                else
                {
                    //Avoid yielding
                    taskSource.SetResult(packet.Body);
                    _pendingCommands.TryRemove(packet.Id, out _);
                }
            }
        }

        /// <summary>
        /// Send a packet to the server.
        /// </summary>
        /// <param name="packet">Packet to send, which will be serialized.</param>
        private async Task SendPacketAsync(RCONPacket packet)
        {
            if (!_connected) throw new InvalidOperationException("Connection is closed.");
            await _tcp.SendAsync(new ArraySegment<byte>(packet.ToBytes()), SocketFlags.None);
            if (packet.Type == PacketType.ExecCommand && _multiPacket)
            {
                //Send a extra packet to find end of large packets
                await _tcp.SendAsync(new ArraySegment<byte>(new RCONPacket(packet.Id, PacketType.Response, "").ToBytes()), SocketFlags.None);
            }

        }
    }
}

