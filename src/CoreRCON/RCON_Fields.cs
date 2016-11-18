using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CoreRCON
{
	public partial class RCON
    {
		private readonly object _locker = new object();

		/// <summary>
		/// When generating the packet ID, use a never-been-used (for automatic packets) ID.
		/// </summary>
		private static int _packetId = 1;

		/// <summary>
		/// Since packets containing the special check string are thrown out, add some randomness to it so users can't add the check string to their name and fly under-the-radar.
		/// </summary>
		private static string _identifier;

		private RCONOptions _options { get; set; }

		// Sockets
		private Socket _tcp { get; set; }
		private UdpClient _udp { get; set; }
		private bool _connected = false;

		/// <summary>
		/// Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
		/// </summary>
		private Dictionary<int, Action<string>> _pendingCommands { get; } = new Dictionary<int, Action<string>>();

		/// <summary>
		/// Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
		/// </summary>
		private TaskCompletionSource<bool> _authenticationTask;

		// Lists of listeners to be polled whenever a packet is received.
		private List<ParserContainer> _parseListeners { get; } = new List<ParserContainer>();
		private List<Action<string>> _rawListeners { get; } = new List<Action<string>>();
		private List<Action<LogAddressPacket>> _logListeners { get; } = new List<Action<LogAddressPacket>>();

		public event Action OnDisconnected;
	}
}
