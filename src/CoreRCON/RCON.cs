using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Buffers;
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
        private readonly object _lock = new object();

        // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
        private TaskCompletionSource<bool> _authenticationTask;

        private bool _connected = false;

        private IPEndPoint _endpoint;

        // When generating the packet ID, use a never-been-used (for automatic packets) ID.
        private int _packetId = 1;

        private string _password;
        private uint _beaconIntervall;
        private int _timeout;
        private bool _multiPacket;

        // Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
        private Dictionary<int, TaskCompletionSource<String>> _pendingCommands { get; } = new Dictionary<int, TaskCompletionSource<String>>();
        private Dictionary<int, string> _incomingBuffer { get; } = new Dictionary<int, string>();

        private Socket _tcp { get; set; }
        private Task _networkConsumerTask;

        public event Action OnDisconnected;

        /// <summary>
        /// Create RCON object, Se main contructor for more info
        /// </summary>
        /// <param name="host">Server adress</param>
        /// <param name="port">Server port</param>
        public RCON(IPAddress host, ushort port, string password, uint beaconIntervall = 30000, uint tcpTimeout = 10000, bool sourceMultiPacketSupport = false)
            : this(new IPEndPoint(host, port), password, beaconIntervall, tcpTimeout, sourceMultiPacketSupport)
        { }

        /// <summary>
        /// Create RCON object 
        /// </summary>
        /// <param name="endpoint">Server to connect to</param>
        /// <param name="password">Rcon password</param>
        /// <param name="beaconIntervall">Intervall in milisecounds to send empty requests to server to check if it is alive. A value of 0 disables beacon requests</param>
        /// <param name="tcpTimeout">TCP socket send and recive timout in milisecounds. A value of 0 means no timeout</param>
        /// <param name="sourceMultiPacketSupport">Enable source engine trick to receive multi packet responses using trick by Koraktor</param>
        public RCON(IPEndPoint endpoint, string password, uint beaconIntervall = 30000, uint tcpTimeout = 0, bool sourceMultiPacketSupport = false)
        {
            _endpoint = endpoint;
            _password = password;
            _beaconIntervall = beaconIntervall;
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
            //_tcp.NoDelay = true;
            await _tcp.ConnectAsync(_endpoint);
            _connected = true;
            Pipe pipe = new Pipe();
            Task writing = FillPipeAsync(pipe.Writer);
            Task reading = ReadPipeAsync(pipe.Reader);

            // Wait for successful authentication
            _authenticationTask = new TaskCompletionSource<bool>();
            await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password));
            _networkConsumerTask = Task.WhenAll(writing, reading);
            await _authenticationTask.Task;
            if (_beaconIntervall != 0)
            {
                Task.Run(() =>
                     WatchForDisconnection(_beaconIntervall).ConfigureAwait(false)
                );
            }
        }

        /// <summary>
        /// Fill pipe with data when availble in the socket
        /// </summary>
        /// <param name="writer"></param>
        /// <returns>Producer Task</returns>
        async Task FillPipeAsync(PipeWriter writer)
        {
            const int minimumBufferSize = Constants.MIN_PACKET_SIZE;

            while (_connected)
            {
                // Allocate at least 14 bytes from the PipeWriter
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await _tcp.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    await writer.FlushAsync();
                    throw ex;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();

        }

        /// <summary>
        /// Read data from pipeline when avalible, constructing new RCON packets 
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
                    // Complete header not yet recived
                }
                int size = BitConverter.ToInt32(buffer.Slice(packetStart, 4).ToArray(), 0);
                Console.WriteLine($"Reciving {size} bytes of packet");
                if (buffer.Length >= size + 4)
                {
                    // Get packet end posisition 
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


            // Mark the PipeReader as complete
            reader.Complete();
        }

        public void Dispose()
        {
            _connected = false;
            _tcp.Shutdown(SocketShutdown.Both);
            _tcp.Dispose();
        }

        /// <summary>
        /// Send a command to the server, and wait for the response before proceeding.  Expect the result to be parseable into T.
        /// </summary>
        /// <typeparam name="T">Type to parse the command as.</typeparam>
        /// <param name="command">Command to send to the server.</param>
        /// <exception cref = "System.FormatException" > Unable to parse response </ exception >
        /// <exception cref = "System.AggregateException" >Connection exceptions</ exception >
        public async Task<T> SendCommandAsync<T>(string command)
            where T : class, IParseable, new()
        {
            string response = await SendCommandAsync(command);
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
            Monitor.Enter(_lock);
            var source = new TaskCompletionSource<string>();
            _pendingCommands.Add(++_packetId, source);
            var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
            Monitor.Exit(_lock);
            await SendPacketAsync(packet);
            await Task.WhenAny(source.Task, _networkConsumerTask);
            if (source.Task.IsCompleted)
            {
                return source.Task.Result;
            }

            throw new AggregateException(new[] { source.Task, _networkConsumerTask }.Select(t => t.Exception));
        }

        /// <summary>
        /// Merges RCON packet bodies and resolves the waiting task
        /// with the full body when full response has been recived. 
        /// </summary>
        /// <param name="packet"> Newly received packet </param>
        private void RCONPacketReceived(RCONPacket packet)
        {
            // Call pending result and remove from map
            TaskCompletionSource<string> taskSource;
            if (_pendingCommands.TryGetValue(packet.Id, out taskSource))
            {
                if (_multiPacket)
                {
                    //Read any previous messgaes 
                    string body;
                    _incomingBuffer.TryGetValue(packet.Id, out body);

                    if (packet.Body == "")
                    {
                        //Avoid yeilding
                        taskSource.SetResult(body ?? string.Empty);
                        _pendingCommands.Remove(packet.Id);
                    }
                    else
                    {
                        //Append to previous messages
                        _incomingBuffer[packet.Id] = body + packet.Body;
                    }
                }
                else
                {
                    //Avoid yeilding
                    taskSource.SetResult(packet.Body);
                    _pendingCommands.Remove(packet.Id);
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
            if (packet.Type == PacketType.ExecCommand && !packet.Body.StartsWith(Constants.CHECK_STR) && _multiPacket)
            {
                //Send a extra packet to find end of large packets
                await _tcp.SendAsync(new ArraySegment<byte>(new RCONPacket(packet.Id, PacketType.Response, "").ToBytes()), SocketFlags.None);
            }

        }



        /// <summary>
        /// Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
        /// </summary>
        /// <param name="delay">Time in milliseconds to wait between polls.</param>
        private async Task WatchForDisconnection(uint delay)
        {
            int checkedDelay = checked((int)delay);

            while (_connected)
            {
                try
                {
                    Identifier = Guid.NewGuid().ToString().Substring(0, 5);
                    await SendCommandAsync(Constants.CHECK_STR + Identifier);
                }
                catch (Exception ex)
                {
                    //Fail waiting messages
                    foreach (var taskPair in _pendingCommands)
                    {
                        taskPair.Value.SetException(ex);
                    }
                    Dispose();
                    OnDisconnected();
                    return;
                }

                await Task.Delay(checkedDelay);
            }
        }
    }


}

