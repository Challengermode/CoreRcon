using CoreRCON.PacketFormats;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CoreRCON
{
	/// <summary>
	/// Make a request to a server following the Source Server Query format.
	/// </summary>
	/// <see cref="https://developer.valvesoftware.com/wiki/Server_queries"/>
	public class ServerQuery
	{
		private static UdpClient _client;

		static ServerQuery()
		{
			_client = new UdpClient();
		}

		/// <summary>
		/// Get information about the server.
		/// </summary>
		/// <param name="address">IP of the server.</param>
		/// <param name="port">Port to query gameserver.</param>
		public static async Task<ServerQueryInfo> Info(IPAddress address, ushort port) => await Info(new IPEndPoint(address, port));

		/// <summary>
		/// Get information about the server.
		/// </summary>
		/// <param name="host">Endpoint of the server.</param>
		public static async Task<ServerQueryInfo> Info(IPEndPoint host)
		{
			await _client.SendAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 }, 25, host);
			var response = await _client.ReceiveAsync();
			return ServerQueryInfo.FromBytes(response.Buffer);
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
			var challenge = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55 }.Concat(await Challenge(host)).ToArray();
			await _client.SendAsync(challenge, 9, host);
			var response = await _client.ReceiveAsync();
			return ServerQueryPlayer.FromBytes(response.Buffer);
		}

		/// <summary>
		/// Send a challenge request to the server and receive a code.
		/// </summary>
		/// <param name="host">Endpoint of the server.</param>
		/// <returns>Challenge code to use with challenged requests.</returns>
		private static async Task<IEnumerable<byte>> Challenge(IPEndPoint host)
		{
			await _client.SendAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF }, 9, host);
			return (await _client.ReceiveAsync()).Buffer.Skip(5).Take(4);
		}
	}
}