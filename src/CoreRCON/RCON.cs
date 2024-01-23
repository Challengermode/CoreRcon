using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using Microsoft.Extensions.Logging;

namespace CoreRCON
{
    /// <summary>
    /// Create RCON object 
    /// </summary>
    /// <param name="endpoint">Server to connect to</param>
    /// <param name="password">Rcon password</param>
    /// <param name="timeout">Timeout to connect and send messages in milliseconds. A value of 0 means no timeout</param>
    /// <param name="sourceMultiPacketSupport">Enable source engine trick to receive multi packet responses using trick by Koraktor</param>
    /// <param name="logger">Logger to use, null means none</param>
    public partial class RCON(
        IPEndPoint endpoint,
        string password,
        uint timeout = 10000,
        bool sourceMultiPacketSupport = false,
        ILogger logger = null) : IDisposable
    {
        // Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
        private TaskCompletionSource<bool> _authenticationTask;

        private bool _connected = false;

        private readonly IPEndPoint _endpoint = endpoint;

        // When generating the packet ID, use a never-been-used (for automatic packets) ID.
        private int _packetId = 0;

        private readonly string _password = password;
        private readonly int _timeout = (int)timeout;
        private readonly bool _multiPacket = sourceMultiPacketSupport;

        // Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
        private ConcurrentDictionary<int, TaskCompletionSource<string>> _pendingCommands { get; } = new ConcurrentDictionary<int, TaskCompletionSource<string>>();
        private Dictionary<int, string> _incomingBuffer { get; } = [];

        private Socket _tcp { get; set; }

        private readonly ILogger _logger = logger;
        readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
        private Task _socketWriter;
        private Task _socketReader;

        /// <summary>
        /// Fired if connection is lost
        /// </summary>
        public event Action OnDisconnected;

        /// <summary>
        /// Fired when an RCON package has been received
        /// </summary>
        public event Action<RCONPacket> OnPacketReceived;

        /// <summary>
        /// Create RCON object, Se main constructor for more info
        /// </summary>
        /// <param name="host">Server address</param>
        /// <param name="port">Server port</param>
        public RCON(IPAddress host, ushort port, string password, uint timeout = 10000,
            bool sourceMultiPacketSupport = false, ILogger logger = null)
            : this(new IPEndPoint(host, port), password, timeout, sourceMultiPacketSupport, logger)
        { }

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

            using var activity = Tracing.ActivitySource.StartActivity("Connect");
            activity?.AddTag(Tracing.Tags.Address, _endpoint.Address.ToString());
            activity?.AddTag(Tracing.Tags.Port, _endpoint.Port.ToString());

            _tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = _timeout,
                SendTimeout = _timeout,
                NoDelay = true
            };
            await _tcp.ConnectAsync(_endpoint)
                .ConfigureAwait(false);
            _connected = true;
            Pipe pipe = new Pipe();

            _socketWriter = FillPipeAsync(pipe.Writer)
                .ContinueWith(LogDisconnect);
            _socketReader = ReadPipeAsync(pipe.Reader)
                .ContinueWith(LogDisconnect);

            // Wait for successful authentication
            _authenticationTask = new TaskCompletionSource<bool>();
            await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password))
                .ConfigureAwait(false);

            try
            {
                await _authenticationTask.Task
                    .TimeoutAfter(TimeSpan.FromMilliseconds(_timeout))
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("Timeout while waiting for authentication response from server");
            }
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
                    int bytesRead = await _tcp.ReceiveAsync(memory, SocketFlags.None)
                        .ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);

                    // Make the data available to the PipeReader
                    FlushResult result = await writer.FlushAsync()
                        .ConfigureAwait(false);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                // Tell the PipeReader that there's no more data coming
                await writer.FlushAsync()
                    .ConfigureAwait(false);
                await writer.CompleteAsync()
                    .ConfigureAwait(false);
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
            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync()
                        .ConfigureAwait(false);
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
                        byte[] byteArr = buffer.Slice(packetStart, packetEnd).ToArray();
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
                        else
                        {
                            // Forward rcon packet to handler
                            RCONPacketReceived(packet);
                        }

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
            }
            finally
            {
                // If authentication did not complete
                _authenticationTask.TrySetException(
                                    new AuthenticationException($"Server did not respond to auth {_tcp.RemoteEndPoint}.")
                                    );

                // Mark the PipeReader as complete
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connected = false;
                _semaphoreSlim?.Dispose();

                if (_tcp != null)
                {
                    _tcp.Shutdown(SocketShutdown.Both);
                    _tcp.Dispose();
                }
            }
        }

        /// <summary>
        /// Send a command to the server, and wait for the response before proceeding.  Expect the result to be parsable into T.
        /// </summary>
        /// <typeparam name="T">Type to parse the command as.</typeparam>
        /// <param name="command">Command to send to the server.</param>
        /// <exception cref = "System.FormatException" > Unable to parse response </ exception >
        /// <exception cref = "System.AggregateException" >Connection exceptions</ exception >
        public async Task<T> SendCommandAsync<T>(string command, TimeSpan? overrideTimeout = null)
            where T : class, IParseable, new()
        {
            string response = await SendCommandAsync(command, overrideTimeout).ConfigureAwait(false);
            // Se comment about TaskCreationOptions.RunContinuationsAsynchronously in SendComandAsync<string>
            var source = new TaskCompletionSource<T>();
            var instance = ParserHelpers.CreateParser<T>();
            var container = new ParserContainer
            {
                IsMatch = instance.IsMatch,
                Parse = instance.Parse,
            };

            if (!container.TryParse(response, out var parsed))
            {
                throw new FormatException("Failed to parse server response");
            }
            return (T)parsed;
        }

        /// <summary>
        /// Send a command to the server, and wait for the response before proceeding. 
        /// </summary>
        /// <param name="command">Command to send to the server.</param>
        /// <exception cref = "System.AggregateException" >Connection exceptions</ exception >
        public async Task<string> SendCommandAsync(string command, TimeSpan? overrideTimeout = null)
        {

            // This TaskCompletion source could be initialized with TaskCreationOptions.RunContinuationsAsynchronously
            // However we this library is designed to be able to run without its own thread
            // Read more about this option here:
            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#always-create-taskcompletionsourcet-with-taskcreationoptionsruncontinuationsasynchronously
            var completionSource = new TaskCompletionSource<string>();
            int packetId = Interlocked.Increment(ref _packetId);
            if (!_pendingCommands.TryAdd(packetId, completionSource))
            {
                throw new SocketException();
            }

            using var activity = Tracing.ActivitySource.StartActivity("SendCommand");
            activity?.AddTag(Tracing.Tags.Address, _endpoint.Address.ToString());
            activity?.AddTag(Tracing.Tags.Port, _endpoint.Port.ToString());
            activity?.AddTag(Tracing.Tags.PacketId, packetId);
            activity?.AddTag(Tracing.Tags.CommandLength, command.Length);
            activity?.AddTag(Tracing.Tags.CommandCount, command.Count(c => c == ';'));
            activity?.AddTag(Tracing.Tags.CommandFirst, new string(command.TakeWhile(c => c != ' ').ToArray()));

            RCONPacket packet = new RCONPacket(packetId, PacketType.ExecCommand, command);

            // ensuer mutal execution of SendPacketAsync and RCONPacketReceived
            await _semaphoreSlim.WaitAsync();
            Task completedTask;
            try
            {
                await SendPacketAsync(packet).ConfigureAwait(false);
                completedTask = await Task.WhenAny(completionSource.Task, _socketWriter, _socketReader)
                    .TimeoutAfter(overrideTimeout.HasValue ? overrideTimeout.Value : TimeSpan.FromMilliseconds(_timeout))
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("Timeout while waiting for response from server");
            }
            finally
            {
                _semaphoreSlim.Release();
                _pendingCommands.TryRemove(packet.Id, out _);
                _incomingBuffer.Remove(packet.Id);
            }

            if (completedTask == completionSource.Task)
            {
                string response = await completionSource.Task;
                activity?.AddTag(Tracing.Tags.ResponseLength, response.Length);
                return response;
            }

            // Observe exception
            await completedTask;
            throw new SocketException();
        }

        /// <summary>
        /// Merges RCON packet bodies and resolves the waiting task
        /// with the full body when full response has been recived. 
        /// </summary>
        /// <param name="packet"> Newly received packet </param>
        private void RCONPacketReceived(RCONPacket packet)
        {
            _logger?.LogTrace("RCON packet received: {}", packet.Id);
            // Call pending result and remove from map
            if (_pendingCommands.TryGetValue(packet.Id, out TaskCompletionSource<string> taskSource))
            {
                if (_multiPacket)
                {
                    //Read any previous messages 
                    _incomingBuffer.TryGetValue(packet.Id, out string body);

                    if (packet.Body == "\0\u0001\0\0")
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
            else
            {
                _logger?.LogWarning("Received packet with no matching command id: {} body: {}", packet.Id, packet.Body);
            }

            OnPacketReceived?.Invoke(packet);
        }

        /// <summary>
        /// Send a packet to the server.
        /// </summary>
        /// <param name="packet">Packet to send, which will be serialized.</param>
        private async Task SendPacketAsync(RCONPacket packet)
        {
            _logger?.LogTrace("Send packet: {}", packet.Id);
            if (!_connected) throw new InvalidOperationException("Connection is closed.");
            await _tcp.SendAsync(new ArraySegment<byte>(packet.ToBytes()), SocketFlags.None)
                .ConfigureAwait(false);
            if (packet.Type == PacketType.ExecCommand && _multiPacket)
            {
                //Send a extra packet to find end of large packets
                var emptyPackage = new RCONPacket(packet.Id, PacketType.Response, "");
                await _tcp.SendAsync(new ArraySegment<byte>(emptyPackage.ToBytes()), SocketFlags.None)
                    .ConfigureAwait(false);
            }

        }

        private void LogDisconnect(Task task)
        {
            if (_connected)
            {
                _logger?.LogError("RCON connection closed");
                if (task.IsFaulted)
                {
                    _logger?.LogError(task.Exception, "conection closed due to exception");
                }
            }
        }
    }
}

