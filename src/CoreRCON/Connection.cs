using System.IO;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

namespace CoreRCON;

public interface IConnection
{
    Task ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);

    Task<int> ReceiveAsync(Memory<byte> responseBuffer);

    Task SendAsync(ReadOnlyMemory<byte> payload);

    Stream GetOrCreateStream();

    bool IsConnected { get; }
    double Timeout { get; }

    void Disconnect();
}

public class Connection : IConnection, IDisposable
{
    private Socket _tcpClient;
    private NetworkStream _streamClient;
    private double _timeout;

    public Connection(int timeout = 5000) 
    {
        _timeout = timeout;
        _tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            ReceiveTimeout = timeout,
            SendTimeout = timeout,
            NoDelay = true
        };

    }

    public Connection(Socket client)
    {
        _tcpClient = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        await _tcpClient.ConnectAsync(endpoint);
        GetOrCreateStream();
    }


    /// <summary>
    /// Write data on the the channel
    /// </summary>
    /// <param name="payload">Payload to be written</param>
    /// <returns>RconRequest's Task</returns>
    public async Task SendAsync(ReadOnlyMemory<byte> payload)
    {
        await _streamClient.WriteAsync(payload);
    }

    /// <summary>
    /// Read data from the channel
    /// </summary>
    /// <param name="responseBuffer">Buffer to be filled</param>
    /// <returns>Number of bytes read</returns>
    public async Task<int> ReceiveAsync(Memory<byte> responseBuffer)
    {
        return await _streamClient.ReadAsync(responseBuffer);
    }

    /// <summary>
    /// Get whether the channel is connected or not
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected ?? false;
    public double Timeout => _timeout;


    public Stream GetOrCreateStream() => _streamClient == null ? _streamClient = new NetworkStream(_tcpClient) : _streamClient;

    /// <summary>
    /// Disconnect the channel
    /// </summary>
    public void Disconnect()
    {
        _tcpClient?.Close();
        Dispose();
    }

    public void Dispose() {
        if (_tcpClient != null)
        {
            _tcpClient.Shutdown(SocketShutdown.Both);
            _tcpClient?.Dispose();
            _streamClient?.Dispose();
        }
        _tcpClient = null;
    }
}
