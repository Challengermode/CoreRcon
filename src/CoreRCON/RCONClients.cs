using System.Net.Sockets;

namespace CoreRCON
{
	internal class RCONClients
	{
		internal Socket TCP { get; set; }
		internal UdpClient UDP { get; set; }

		internal void Reset(int udpPort = 7744)
		{
			TCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			UDP = new UdpClient(udpPort);
		}
	}
}