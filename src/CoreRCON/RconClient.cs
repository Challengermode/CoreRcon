using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using Microsoft.Extensions.Logging;

namespace CoreRCON;

public class RconClient : IDisposable
{
    private const int BaseSize = sizeof(int) * 2 + sizeof(byte) * 2;
    private const int MinimumBufferSize = sizeof(int) + BaseSize;

    // Map of pending command references.
    // These are called when a command with the matching Id (key) is received.
    // Commands are called only once.
    // Allows us to keep track of when a task succeeds
    private ConcurrentDictionary<int, RconRequest> queuedRequests = new();

    private int _packetId = 1;
    private SemaphoreSlim _semaphoreSlim;
    Random rnd = new Random();

    private readonly IConnection _client;
    private readonly ILogger _logger;
    private Pipe _pipe = new();

    public Action OnCompletion;
    public Action OnDisconnected;


    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Create RCON object, Se main constructor for more info
    /// </summary>
    /// <param name="host">Server address</param>
    /// <param name="port">Server port</param>
    public RconClient(int timeout = 5000) { 
        _client = new Connection(timeout);
        _semaphoreSlim = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Create RCON object 
    /// </summary>
    /// <param name="endpoint">Server to connect to</param>
    /// <param name="password">Rcon password</param>
    /// <param name="timeout">Timeout to connect and send messages in milliseconds. A value of 0 means no timeout</param>
    /// <param name="sourceMultiPacketSupport">Enable source engine trick to receive multi command responses using trick by Koraktor</param>
    /// <param name="logger">Logger to use, null means none</param>
    public RconClient(IConnection client, ILogger logger = null)
    {
        _client = client;
        _logger = logger;
    }

    public RconClient WithEncoding(Encoding encoding)
    {
        this.Encoding = encoding;
        return this;
    }

    public void Dispose()
    {
        (_client as IDisposable)?.Dispose();
        _semaphoreSlim?.Dispose();
    }

    /// <summary>
    /// Connect to a server through RCON.  Automatically sends the authentication command.
    /// </summary>
    /// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
    public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            return;
        }
        await _client.ConnectAsync(endpoint, cancellationToken);

        Task readerTask = ReadFromPipeAsync(_pipe.Reader, cancellationToken);;
        Task writerTask = WriteToPipeAsync(_pipe.Writer, cancellationToken);
        _ = Task.WhenAll(readerTask, writerTask).ContinueWith(t =>
        {
            _pipe.Reset();
            foreach (var command in queuedRequests.ToList())
            {
                command.Value.TaskCompletionSource.SetCanceled();
            }
            Dispose();
        });
    }

    public async Task<bool> AuthenticateAsync(string password)
    {
        if (!_client.IsConnected)
        {
            throw new SocketException(10053);
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password parameter must be a non null non empty string");
        }

        try
        {
            // Ensure mutual execution of SendToServer
            await SendPacketAsync(RconPacket.Create(0, PacketType.Auth, password), expectMultiPacket: false);
        }
        catch (AuthenticationException)
        {
            return false;
        }
        return true;
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
        string response = await SendCommandAsync(command).ConfigureAwait(false);

        // Se comment about TaskCreationOptions.RunContinuationsAsynchronously in SendComandAsync<string>
        var source = new TaskCompletionSource<T>();
        var instance = ParserHelpers.CreateParser<T>();
        var container = new ParserContainer
        {
            IsMatch = line => instance.IsMatch(line),
            Parse = line => instance.Parse(line),
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
    public async Task<string> SendCommandAsync(string command, bool multipacket = false, TimeSpan? overrideTimeout = null)
    {
        await _semaphoreSlim?.WaitAsync();
        try
        {
            // Ensure mutual execution of SendToServer
            int packetId = rnd.Next();//Interlocked.Increment(ref _packetId);
            RconPacket packet = new RconPacket(packetId, PacketType.ExecCommand, command);

            return await SendPacketAsync(packet, multipacket, overrideTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("Timeout while waiting for response from server");
        }
        finally
        {
            _semaphoreSlim?.Release();
        }
    }
    /// <summary>
    /// Send a message encapsulated into an Rcon packet and get the response
    /// </summary>
    /// <param name="packet">Packet to be sent</param>
    /// <returns>The response to this command</returns>
    private async Task<string> SendPacketAsync(RconPacket packet, bool expectMultiPacket, TimeSpan? overrideTimeout = null)
    {
        RconRequest request = new RconRequest(packet, expectMultiPacket);
        queuedRequests.TryAdd(packet.Id, request);
        try
        {
            await _client.SendAsync(packet.ToBytes(Encoding));
            if (expectMultiPacket)
            {
                await _client.SendAsync(packet.Termination.ToBytes(Encoding));
            }
        }
        catch (TimeoutException)
        {
            queuedRequests.TryRemove(packet.Id, out var _);
            throw new TimeoutException("Timeout while waiting for response from server");
        }

        return await request.TaskCompletionSource.Task.TimeoutAfter(overrideTimeout.HasValue 
            ? overrideTimeout : 
            TimeSpan.FromMilliseconds(_client.Timeout));

        throw new SocketException();
    }


    /// <summary>
    /// Read data from the channel and parse it into a RconPacket
    /// </summary>
    public async Task ReadFromPipeAsync(PipeReader reader, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            ReadResult readResult = await reader.ReadAtLeastAsync(MinimumBufferSize, cancellationToken);
            ReadOnlySequence<byte> buffer = readResult.Buffer;
            SequencePosition startPosition = buffer.Start;
            if (buffer.Length < 4) // not enough bytes to get the packet length, need to read more
            {
                if (readResult.IsCompleted)
                {
                    break;
                }

                reader.AdvanceTo(startPosition, buffer.End);
                continue;
            }

            // Read packet size
            int bodySize = BitConverter.ToInt32(buffer.Slice(startPosition, 4).ToArray());
            if (buffer.Length >= bodySize + 4)
            {
                var endPosition = buffer.GetPosition(bodySize + 4, startPosition);
                RconPacket rconPacket = RconPacket.FromBytes(buffer.Slice(startPosition, endPosition), Encoding);
                if(queuedRequests.TryGetValue(rconPacket.Id, out RconRequest matchedCommand))
                {
                    switch (rconPacket.Type)
                    {
                        case PacketType.Response:

                            if(!rconPacket.IsTermination)
                            {
                                matchedCommand.Add(rconPacket);

                                if (matchedCommand.IsMultiPacket)
                                {
                                    // If we are expecting a multi packet response,
                                    // we need to wait for the termination packet
                                    // before sending the full response to the caller 
                                    break;
                                }
                            }

                            // If we reached the end of a response we can send the full response to the caller
                            queuedRequests.Remove(matchedCommand.Request.Id, out var _);
                            matchedCommand.TaskCompletionSource.SetResult(matchedCommand.Body);
                            break;
                        case PacketType.AuthResponse:
                            if (rconPacket.Id == -1)
                            {
                                queuedRequests.Remove(0, out var authRequest);
                                authRequest.TaskCompletionSource.SetException(new AuthenticationException("Invalid password"));
                            }
                            queuedRequests.Remove(matchedCommand.Request.Id, out var _);
                            matchedCommand.TaskCompletionSource.SetResult(rconPacket.Body);
                            break;
                        case PacketType.Auth:
                        default:
                            break;
                    };
                }
                reader.AdvanceTo(endPosition);

            } else
            {
                reader.AdvanceTo(startPosition, buffer.End);
            }


            if (buffer.IsEmpty && readResult.IsCompleted)
            {
                break;
            };
        }
        await reader.CompleteAsync();
    }

    /// <summary>
    /// Read data from the channel and parse it into a RconPacket
    /// </summary>
    public async Task WriteToPipeAsync(PipeWriter writer, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var buffer = writer.GetMemory(14);
            try
            {
                var bytesCount = await _client.ReceiveAsync(buffer);
                if (bytesCount == 0)
                {
                    break;
                }

                writer.Advance(bytesCount);
            }
            catch (Exception ex)
            {
                break;
            }

            // Flush the stream to ensure all data is sent
            var flushResult = await writer.FlushAsync(cancellationToken);
            if (flushResult.IsCompleted)
            {
                break;
            }
        }
        // Tell the _pipe reader that there's no more data coming
        await writer.CompleteAsync();
    }

}

public class RconRequest(RconPacket request, bool multiPacket = false)
{
    public TaskCompletionSource<string> TaskCompletionSource { get; } = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    public RconPacket Request { get; } = request;

    public bool IsMultiPacket { get; } = multiPacket;

    private readonly List<RconPacket> PacketsBuffer = [];

    public void Add(RconPacket packet) => PacketsBuffer.Add(packet);

    public string Body => string.Concat(PacketsBuffer.Select(b => b.Body));
}
