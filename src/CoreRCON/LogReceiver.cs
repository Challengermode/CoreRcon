using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
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
		private IPEndPoint[] _sources { get; set; }

		/// <summary>
		/// Opens a socket to receive LogAddress logs, and registers it with the server.  The IP can also be a local IP if the server is on the same network.
		/// </summary>
		/// <param name="port">Local port to bind to.</param>
		/// <param name="sources">Array of endpoints to accept logaddress packets from.  The server you plan on receiving packets from must be in this list.</param>
		public LogReceiver(ushort port, params IPEndPoint[] sources)
		{
			_sources = sources;
			_udp = new UdpClient(port);
			Task.Run(() => StartListener());
		}

		public void Dispose()
		{
			_udp.Dispose();
		}

		/// <summary>
		/// Listens on the socket for a parseable class to read.
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
		/// </summary>
		/// <param name="result">Parsed LogAddress packet.</param>
		public void ListenPacket(Action<LogAddressPacket> result) => _logListeners.Add(result);

		/// <summary>
		/// Listens on the socket for anything, returning just the body of the packet.
		/// </summary>
		/// <param name="result">Raw string returned by the server.</param>
		public void ListenRaw(Action<string> result) => _rawListeners.Add(result);

		private void LogAddressPacketReceived(LogAddressPacket packet)
		{

			// Call LogAddress listeners
			foreach (var listener in _logListeners)
				listener?.Invoke(packet);

			string body = packet.Body;
			if (body.Length == 0) return;

			// Call parsers
			foreach (var parser in _parseListeners)
				parser.TryCallback(body);

			// Call raw listeners
			foreach (var listener in _rawListeners)
				listener?.Invoke(body);
		}

		private async Task StartListener()
		{
			while (true)
			{
				var result = await _udp.ReceiveAsync();

				// If the packet did not come from an accepted source, throw it out
				if (!_sources.Contains(result.RemoteEndPoint)) return;

				// Parse out the LogAddress packet
				LogAddressPacket packet = LogAddressPacket.FromBytes(result.Buffer);
				LogAddressPacketReceived(packet);
			}
		}
	}
}