using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
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
        private uint _reconnectDelay;

        // Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
        private Dictionary<int, Action<string>> _pendingCommands { get; } = new Dictionary<int, Action<string>>();

        private Socket _tcp { get; set; }

        public event Action OnDisconnected;

        /// <summary>
        /// Initialize an RCON connection and automatically call ConnectAsync().
        /// </summary>
        public RCON(IPAddress host, ushort port, string password, uint reconnectDelay = 30000)
            : this(new IPEndPoint(host, port), password, reconnectDelay)
        { }

        /// <summary>
        /// Initialize an RCON connection and automatically call ConnectAsync().
        /// </summary>
        public RCON(IPEndPoint endpoint, string password, uint reconnectDelay = 30000)
        {
            _endpoint = endpoint;
            _password = password;
            _reconnectDelay = reconnectDelay;
            ConnectAsync().Wait();
        }

        /// <summary>
        /// Connect to a server through RCON.  Automatically sends the authentication packet.
        /// </summary>
        /// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
        public async Task ConnectAsync()
        {
            _tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await _tcp.ConnectAsync(_endpoint);
            _connected = true;
            Pipe pipe = new Pipe();
            Task writing = FillPipeAsync(_tcp, pipe.Writer);
            Task reading = ReadPipeAsync(pipe.Reader);

            // Wait for successful authentication
            _authenticationTask = new TaskCompletionSource<bool>();
            await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password));
            await _authenticationTask.Task;

            Task.Run(() => WatchForDisconnection(_reconnectDelay)).Forget();
        }

        public static Task<int> ReceiveAsync(Socket socket, Memory<byte> memory, SocketFlags socketFlags)
        {
            var arraySegment = GetArray(memory);
            return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
        }

        private static ArraySegment<byte> GetArray(Memory<byte> memory)
        {
            return GetArray((ReadOnlyMemory<byte>)memory);
        }

        private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
        {
            if (!MemoryMarshal.TryGetArray(memory, out var result))
            {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }


        async Task FillPipeAsync(Socket socket, PipeWriter writer)
        {
            const int minimumBufferSize = Constants.MIN_PACKET_SIZE;

            while (true)
            {
                // Allocate at least 14 bytes from the PipeWriter
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await ReceiveAsync(socket, memory, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    break;
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

        async Task ReadPipeAsync(PipeReader reader)
        {
            byte[] byteArr = new byte[Constants.MAX_PACKET_SIZE];
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;
                SequencePosition position = buffer.Start;

                if (buffer.Length < 4)
                {
                    if (result.IsCompleted)
                    {
                        break;
                    }
                    reader.AdvanceTo(position, buffer.End);
                    Console.WriteLine("Header not complete");
                    continue;
                    // Complete header not yet recived
                }
                int size = BitConverter.ToInt32(buffer.Slice(position, 4).ToArray(), 0);
                Console.WriteLine($"Reciving {size} bytes of packet");
                if (buffer.Length >= size + 4)
                {
                    byteArr = buffer.Slice(position, size + 4).ToArray();
                    RCONPacket packet = RCONPacket.FromBytes(byteArr);

                    if (packet.Type == PacketType.AuthResponse)
                    {
                        // Failed auth responses return with an ID of -1
                        if (packet.Id == -1)
                        {
                            throw new AuthenticationException($"Authentication failed for {_tcp.RemoteEndPoint}.");
                        }
                        // Tell Connect that authentication succeeded
                        _authenticationTask.SetResult(true);
                    }

                    // Forward rcon packet to handler
                    RCONPacketReceived(packet);

                    // Advance buffer position
                    position = buffer.GetPosition(size + 4, position);
                    reader.AdvanceTo(position);
                }
                else
                {
                    reader.AdvanceTo(position, buffer.End);
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
        public Task<T> SendCommandAsync<T>(string command)
            where T : class, IParseable, new()
        {
            Monitor.Enter(_lock);
            var source = new TaskCompletionSource<T>();
            var instance = ParserHelpers.CreateParser<T>();

            var container = new ParserContainer
            {
                IsMatch = line => instance.IsMatch(line),
                Parse = line => instance.Parse(line),
                Callback = parsed => source.SetResult((T)parsed)
            };

            _pendingCommands.Add(++_packetId, container.TryCallback);
            var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
            Console.Write(packet.Id);
            Console.WriteLine(packet.Body);

            Monitor.Exit(_lock);

            SendPacketAsync(packet);
            return source.Task;

        }

        /// <summary>
        /// Send a command to the server, and wait for the response before proceeding.  R
        /// </summary>
        /// <param name="command">Command to send to the server.</param>
        public Task<string> SendCommandAsync(string command)
        {
            Monitor.Enter(_lock);
            var source = new TaskCompletionSource<string>();
            _pendingCommands.Add(++_packetId, source.SetResult);
            var packet = new RCONPacket(_packetId, PacketType.ExecCommand, command);
            Monitor.Exit(_lock);

            SendPacketAsync(packet);
            return source.Task;
        }

        private void RCONPacketReceived(RCONPacket packet)
        {
            // Call pending result and remove from map
            Action<string> action;
            if (_pendingCommands.TryGetValue(packet.Id, out action))
            {
                //Make sure that we don't yeild to the main thread. 
                Task.Run(() => { action?.Invoke(packet.Body); }).Forget();
                _pendingCommands.Remove(packet.Id);
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
        }



        /// <summary>
        /// Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
        /// </summary>
        /// <param name="delay">Time in milliseconds to wait between polls.</param>
        private async Task WatchForDisconnection(uint delay)
        {
            int checkedDelay = checked((int)delay);

            while (true)
            {
                try
                {
                    Identifier = Guid.NewGuid().ToString().Substring(0, 5);
                    await SendCommandAsync(Constants.CHECK_STR + Identifier);
                }
                catch (Exception ex)
                {
                    Dispose();
                    OnDisconnected();
                    return;
                }

                await Task.Delay(checkedDelay);
            }
        }
    }


}

