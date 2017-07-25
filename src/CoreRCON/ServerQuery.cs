using CoreRCON.PacketFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CoreRCON
{
    /// <summary>
    /// Make a request to a server following the Source and Minecraft Server Query format.
    /// </summary>
    /// <see cref="https://developer.valvesoftware.com/wiki/Server_queries"/>
    /// <see cref="http://wiki.vg/Query"/>
    public class ServerQuery
	{
        /// <summary>
        /// The different query implementations.
        /// </summary>
        public enum ServerType {
            Source,
            Minecraft
        }

        /// <summary>
        /// Minecraft packet types.
        /// </summary>
        private enum PacketType
        {
            Handshake = 0x09,
            Stat = 0x00
        }

		private static UdpClient _client;
        private static readonly byte[] _magic = new byte[] { 0xFE, 0xFD }; // Minecraft 'magic' bytes.
        private static readonly byte[] _sessionid = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        static ServerQuery()
		{
			_client = new UdpClient();
		}

		/// <summary>
		/// Get information about the server.
		/// </summary>
		/// <param name="address">IP of the server.</param>
		/// <param name="port">Port to query gameserver.</param>
        /// <param name="type">Server type.</param>
		public static async Task<IQueryInfo> Info(IPAddress address, ushort port, ServerType type) => await Info(new IPEndPoint(address, port), type);

		/// <summary>
		/// Get information about the server.
		/// </summary>
		/// <param name="host">Endpoint of the server.</param>
		public static async Task<IQueryInfo> Info(IPEndPoint host, ServerType type)
		{
            switch (type)
            {
                case ServerType.Source:
                    await _client.SendAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 }, 25, host);
                    var sourceResponse = await _client.ReceiveAsync();
                    return SourceQueryInfo.FromBytes(sourceResponse.Buffer);
                case ServerType.Minecraft:
                    var padding = new byte[] { 0x00, 0x00, 0x00, 0x00 };
                    var datagram = _magic.Concat(new[] { (byte)PacketType.Stat }).Concat(_sessionid).Concat(await Challenge(host, ServerType.Minecraft)).Concat(padding).ToArray();
                    await _client.SendAsync(datagram, datagram.Length, host);
                    var mcResponce = await _client.ReceiveAsync();
                    return MinecraftQueryInfo.FromBytes(mcResponce.Buffer);
                default:
                    throw new ArgumentException("type argument was invalid");
            }
			
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
			var challenge = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55 }.Concat(await Challenge(host, ServerType.Source)).ToArray();
			await _client.SendAsync(challenge, 9, host);
			var response = await _client.ReceiveAsync();
			return ServerQueryPlayer.FromBytes(response.Buffer);
		}

		/// <summary>
		/// Send a challenge request to the server and receive a code.
		/// </summary>
		/// <param name="host">Endpoint of the server.</param>
		/// <returns>Challenge code to use with challenged requests.</returns>
		private static async Task<byte[]> Challenge(IPEndPoint host, ServerType type)
		{
            switch (type)
            {
                case ServerType.Source:
                    await _client.SendAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF }, 9, host);
                    return (await _client.ReceiveAsync()).Buffer.Skip(5).Take(4).ToArray();
                case ServerType.Minecraft:
                    // Create request
                    var datagram = _magic.Concat(new[] { (byte)PacketType.Handshake }).Concat(_sessionid).ToArray();
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