using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CoreRCON.PacketFormats;

namespace CoreRCON
{
    /// <summary>
    /// Make a request to a server following the Source and Minecraft Server Query format.
    /// </summary>
    /// <see href="https://developer.valvesoftware.com/wiki/Server_queries"/>
    /// <see href="http://wiki.vg/Query"/>
    /// <see href="https://wiki.vg/Server_List_Ping"/>
    public static class ServerQuery
    {
        /// <summary>
        /// The different query implementations.
        /// </summary>
        public enum ServerType
        {
            Source,
            Minecraft
        }

        /// <summary>
        /// Minecraft packet types.
        /// </summary>
        private enum PacketType : byte
        {
            Handshake = 0x09,
            Stat = 0x00
        }

        private static readonly UdpClient _client;
        private static readonly byte[] _magic = [0xFE, 0xFD]; 
        private static readonly byte[] _asInfoPayload = [0xFF, 0xFF, 0xFF, 0xFF, 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00];
        private static readonly byte[] _asInfochallengeResponse = [0xFF, 0xFF, 0xFF, 0xFF, 0x41];

        static ServerQuery()
        {
            _client = new UdpClient();
        }

        /// <inheritdoc cref="Info(IPEndPoint, ServerType, TimeSpan?)"/>
        /// <param name="address">IP of the server.</param>
        public static async Task<IQueryInfo> Info(IPAddress address, ushort port, ServerType type, TimeSpan? timeout = null)
            => await Info(new IPEndPoint(address, port), type, timeout);

        /// <summary>
        /// Get information about the server.
        /// </summary>
        /// <param name="host">Endpoint of the server.</param>
        /// <param name="type">Server type.</param>
        /// <param name="timeout">Server type.</param>
        /// <exception cref="TimeoutException"></exception>
        public static Task<IQueryInfo> Info(IPEndPoint host, ServerType type, TimeSpan? timeout = null)
        {
            return Task.Run<IQueryInfo>(async () =>
            {
                switch (type)
                {
                    case ServerType.Source:
                        await _client.SendAsync(_asInfoPayload, _asInfoPayload.Length, host);
                        UdpReceiveResult sourceResponse = await _client.ReceiveAsync();
                        // If Server responds with a Challenge number we need to resend the request with that number
                        if (sourceResponse.Buffer.ToArray().Take(_asInfochallengeResponse.Length).SequenceEqual(_asInfochallengeResponse))
                        {
                            byte[] challenge = [.. _asInfoPayload, .. sourceResponse.Buffer.Skip(5).Take(4)];
                            await _client.SendAsync(challenge, challenge.Length, host);
                            sourceResponse = await _client.ReceiveAsync();
                        }
                        return SourceQueryInfo.FromBytes(sourceResponse.Buffer);
                    case ServerType.Minecraft:

                        Random random = new();
                        int sessionId = random.Next() & 0x0F0F0F0F; // Minecraft does not process the higher 4-bits on each byte 
                        byte[] sessionIdByte = BitConverter.GetBytes(sessionId);
                        byte[] padding = [0x00, 0x00, 0x00, 0x00];
                        byte[] challengeResponse = await Challenge(host, ServerType.Minecraft, sessionIdByte);
                        byte[] datagram = [.. _magic, (byte)PacketType.Stat, .. sessionIdByte, .. challengeResponse, .. padding];

                        await _client.SendAsync(datagram, datagram.Length, host);
                        UdpReceiveResult mcResponce = await _client.ReceiveAsync();
                        return MinecraftQueryInfo.FromBytes(mcResponce.Buffer);
                    default:
                        throw new ArgumentException("type argument was invalid");
                }
            }).TimeoutAfter(timeout);
        }

        /// <summary>
        /// Get information about each player currently on the server.
        /// </summary>
        /// <param name="address">IP of the server.</param>
        /// <param name="port">Port to query gameserver.</param>
        public static async Task<ServerQueryPlayer[]> Players(IPAddress address, ushort port) => await Players(new IPEndPoint(address, port));

        /// <summary>
        /// Get information about each player currently on the server.
        /// </summary>
        /// <param name="host">Endpoint of the server.</param>
        public static async Task<ServerQueryPlayer[]> Players(IPEndPoint host)
        {
            byte[] challenge = [0xFF, 0xFF, 0xFF, 0xFF, 0x55, .. await Challenge(host, ServerType.Source)];
            await _client.SendAsync(challenge, 9, host);
            UdpReceiveResult response = await _client.ReceiveAsync();
            return ServerQueryPlayer.FromBytes(response.Buffer);
        }

        /// <summary>
        /// Send a challenge request to the server and receive a code.
        /// </summary>
        /// <param name="host">Endpoint of the server.</param>
        /// <returns>Challenge code to use with challenged requests.</returns>
        private static async Task<byte[]> Challenge(IPEndPoint host, ServerType type, byte[] seesionId = null)
        {
            switch (type)
            {
                case ServerType.Source:
                    await _client.SendAsync([0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF], 9, host);
                    return (await _client.ReceiveAsync()).Buffer.Skip(5).Take(4).ToArray();
                case ServerType.Minecraft:
                    // Create request
                    byte[] datagram = [.. _magic, (byte)PacketType.Handshake, .. seesionId];
                    await _client.SendAsync(datagram, datagram.Length, host);

                    // Parse challenge token
                    var buffer = (await _client.ReceiveAsync()).Buffer;
                    var challangeBytes = new byte[16];
                    Array.Copy(buffer, 5, challangeBytes, 0, buffer.Length - 5);
                    var challengeInt = int.Parse(Encoding.ASCII.GetString(challangeBytes));
                    return BitConverter.GetBytes(challengeInt).Reverse().ToArray();
                default:
                    throw new ArgumentException("type argument was invalid");
            }

        }
    }
}
