using System.Net.Sockets;

namespace CoreRCON
{
	internal class RCONClients
	{
		internal Socket TCP { get; set; }
		internal UdpClient UDP { get; set; }

		internal void Reset(ushort udpPort)
		{
			TCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			UDP = new UdpClient(udpPort);
		}
	}
}