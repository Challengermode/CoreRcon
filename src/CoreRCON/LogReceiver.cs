using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CoreRCON
{
	public class LogReceiver : IDisposable
	{
		public int ResolvedPort
		{
			get
			{
				if (_udp == null) return 0;
				return ((IPEndPoint)(_udp.Client.LocalEndPoint)).Port;
			}
		}

		private List<Action<LogAddressPacket>> _logListeners { get; } = new List<Action<LogAddressPacket>>();
		private List<ParserContainer> _parseListeners { get; } = new List<ParserContainer>();
		private List<Action<string>> _rawListeners { get; } = new List<Action<string>>();
		private UdpClient _udp { get; set; }

		/// <summary>
		/// Opens a socket to receive LogAddress logs, and registers it with the server.  The IP can also be a local IP if the server is on the same network.
		/// </summary>
		public LogReceiver(IPAddress self, ushort port)
		{
			_udp = new UdpClient(port);
			Task.Run(() => StartListener());
		}

		public void Dispose()
		{
			_udp.Dispose();
		}

		/// <summary>
		/// Listens on the socket for a parseable class to read.
		/// Most useful with StartLogging(), though this will also fire when a response is received from the TCP connection.
		/// </summary>
		/// <typeparam name="T">Class to be parsed; must have a ParserAttribute.</typeparam>
		/// <param name="result">Parsed class.</param>
		public void Listen<T>(Action<T> result)
			where T : class, IParseable, new()
		{
			// Instantiate the parser associated with the type parameter
			var instance = ParserHelpers.CreateParser<T>();

			// Create the parser container
			_parseListeners.Add(new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => result((T)parsed)
			});
		}

		/// <summary>
		/// Listens on the socket for anything from LogAddress, returning the full packet.
		/// StartLogging() should be run with this.
		/// </summary>
		/// <param name="result">Parsed LogAddress packet.</param>
		public void ListenPacket(Action<LogAddressPacket> result)
		{
			_logListeners.Add(result);
		}

		/// <summary>
		/// Listens on the socket for anything, returning just the body of the packet.
		/// Most useful with StartLogging(), though this will also fire when a response is received from the TCP connection.
		/// </summary>
		/// <param name="result">Raw string returned by the server.</param>
		public void ListenRaw(Action<string> result)
		{
			_rawListeners.Add(result);
		}

		private void CallListeners(string body)
		{
			if (body.Length < 1) return;

			// Call parsers
			foreach (var parser in _parseListeners)
				parser.TryCallback(body);

			// Call raw listeners
			foreach (var listener in _rawListeners)
				listener?.Invoke(body);
		}

		private void LogAddressPacketReceived(LogAddressPacket packet)
		{
			// Filter out checks
			if (packet.Body.Contains(Constants.CHECK_STR + RCON._identifier))
				return;

			// Call LogAddress listeners
			foreach (var listener in _logListeners)
				listener?.Invoke(packet);

			// Lower priority
			CallListeners(packet.RawBody);
		}

		private async Task StartListener()
		{
			while (true)
			{
				var result = await _udp.ReceiveAsync();

				// Parse out the LogAddress packet
				LogAddressPacket packet = LogAddressPacket.FromBytes(result.Buffer);
				LogAddressPacketReceived(packet);
			}
		}
	}
}