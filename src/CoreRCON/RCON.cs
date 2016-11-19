using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CoreRCON
{
	public partial class RCON : IDisposable
	{
		/// <summary>
		/// Since packets containing the special check string are thrown out, add some randomness to it so users can't add the check string to their name and fly under-the-radar.
		/// </summary>
		private static string _identifier;

		/// <summary>
		/// When generating the packet ID, use a never-been-used (for automatic packets) ID.
		/// </summary>
		private static int _packetId = 1;

		private readonly object _locker = new object();

		/// <summary>
		/// Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
		/// </summary>
		private TaskCompletionSource<bool> _authenticationTask;

		private bool _connected = false;

		private List<Action<LogAddressPacket>> _logListeners { get; } = new List<Action<LogAddressPacket>>();

		private RCONOptions _options { get; set; }

		// Lists of listeners to be polled whenever a packet is received.
		private List<ParserContainer> _parseListeners { get; } = new List<ParserContainer>();

		/// <summary>
		/// Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
		/// </summary>
		private Dictionary<int, Action<string>> _pendingCommands { get; } = new Dictionary<int, Action<string>>();

		private List<Action<string>> _rawListeners { get; } = new List<Action<string>>();

		// Sockets
		private Socket _tcp { get; set; }

		private UdpClient _udp { get; set; }

		public event Action OnDisconnected;

		public RCON(RCONOptions options)
		{
			if (options.ServerHost == null) throw new NullReferenceException("Server hostname cannot be null.");
			if (options.Password == null) throw new NullReferenceException("Password cannot be null (authentication will always fail).");

			_options = options;
		}

		/// <summary>
		/// Connect to a server through RCON.  Automatically sends the authentication packet.
		/// </summary>
		/// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
		public async Task ConnectAsync()
		{
			_tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			await _tcp.ConnectAsync(_options.ServerHost, _options.ServerPort);
			_connected = true;

			// Set up TCP listener
			var e = new SocketAsyncEventArgs();
			e.Completed += TCPPacketReceived;
			e.SetBuffer(new byte[Constants.MAX_PACKET_SIZE], 0, Constants.MAX_PACKET_SIZE); // TODO this may not be resetting each time, should be a new buffer every time

			// Start listening for responses
			_tcp.ReceiveAsync(e);

			// Wait for successful authentication
			_authenticationTask = new TaskCompletionSource<bool>();
			await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _options.Password));
			await _authenticationTask.Task;

			if (_options.EnableLogging)
			{
				Task.Run(() => StartLogging());
			}

			Task.Run(() => WatchForDisconnection(_options.DisconnectionCheckInterval));
		}

		public void Dispose()
		{
			lock (_locker)
			{
				_connected = false;
				_tcp.Shutdown(SocketShutdown.Both);
				_tcp.Dispose();
				_udp.Dispose();
			}
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
			var instance = ParserHelpers.GetParser<T>();

			// Create the parser container
			_parseListeners.Add(new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => result((T)parsed)
			});
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
		/// Send a command to the server, and call the result when a response is received.
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		/// <param name="result">Response from the server.</param>
		public void SendCommand(string command, Action<string> result = null)
		{
			var packet = new RCONPacket(++_packetId, PacketType.ExecCommand, command);
			_pendingCommands.Add(_packetId, result);
			SendPacketAsync(packet).Wait();
		}

		/// <summary>
		/// Send a command to the server, and call the result when a response is received.
		/// </summary>
		/// <typeparam name="T">Type to parse the command as.</typeparam>
		/// <param name="command">Command to send to the server.</param>
		/// <param name="result">Strongly typed response from the server.</param>
		public void SendCommand<T>(string command, Action<T> result)
			where T : class, IParseable, new()
		{
			var instance = ParserHelpers.GetParser<T>();
			var container = new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => result((T)parsed)
			};

			var packet = new RCONPacket(++_packetId, PacketType.ExecCommand, command);
			_pendingCommands.Add(_packetId, container.TryCallback);
			SendPacketAsync(packet).Wait();
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.
		/// </summary>
		/// <typeparam name="T">Type to parse the command as.</typeparam>
		/// <param name="command">Command to send to the server.</param>
		public async Task<T> SendCommandAsync<T>(string command)
			where T : class, IParseable, new()
		{
			var source = new TaskCompletionSource<T>();
			SendCommand<T>(command, source.SetResult);
			return await source.Task;
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		public async Task<string> SendCommandAsync(string command)
		{
			var source = new TaskCompletionSource<string>();
			SendCommand(command, source.SetResult);
			return await source.Task;
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
			if (packet.Body.Contains(Constants.CHECK_STR + _identifier))
				return;

			// Call LogAddress listeners
			foreach (var listener in _logListeners)
				listener?.Invoke(packet);

			// Lower priority
			CallListeners(packet.RawBody);
		}

		private void RCONPacketReceived(RCONPacket packet)
		{
			// Call pending result and remove from map
			Action<string> action;
			if (_pendingCommands.TryGetValue(packet.Id, out action))
			{
				action?.Invoke(packet.Body);
				_pendingCommands.Remove(packet.Id);
			}

			CallListeners(packet.Body);
		}

		/// <summary>
		/// Send a packet to the server.
		/// </summary>
		/// <param name="packet">Packet to send, which will be serialized.</param>
		private async Task SendPacketAsync(RCONPacket packet)
		{
			if (!_connected) throw new InvalidOperationException("Connection is closed.");
			await _tcp.SendAsync(new ArraySegment<byte>(packet.ToBytes()), SocketFlags.None);
			await Task.Delay(100);
		}

		/// <summary>
		/// Opens a socket to receive LogAddress logs, and registers it with the server.  The IP can also be a local IP if the server is on the same network.
		/// </summary>
		private async void StartLogging()
		{
			_udp = new UdpClient(_options.LogPort);
			int resolvedPort = ((IPEndPoint)(_udp.Client.LocalEndPoint)).Port;

			// Add the UDP client to logaddress
			SendCommand($"logaddress_add {_options.LogHost}:{resolvedPort}");

			while (true)
			{
				var result = await _udp.ReceiveAsync();

				// Parse out the LogAddress packet
				LogAddressPacket packet = LogAddressPacket.FromBytes(result.Buffer);
				LogAddressPacketReceived(packet);
			}
		}

		/// <summary>
		/// Event called whenever raw data is received on the TCP socket.
		/// </summary>
		private void TCPPacketReceived(object sender, SocketAsyncEventArgs e)
		{
			// Parse out the actual RCON packet
			RCONPacket packet = RCONPacket.FromBytes(e.Buffer);

			if (packet.Type == PacketType.AuthResponse)
			{
				// Failed auth responses return with an ID of -1
				if (packet.Id == -1)
				{
					throw new AuthenticationException($"Authentication failed for {_tcp.RemoteEndPoint}.");
				}

				// Tell Connect that authentication succeeded
				_authenticationTask.SetResult(true);
			}

			// Forward to handler
			RCONPacketReceived(packet);

			// Continue listening
			if (!_connected) return;
			_tcp.ReceiveAsync(e);
		}

		/// <summary>
		/// Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
		/// </summary>
		/// <param name="delay">Time in milliseconds to wait between polls.</param>
		private async void WatchForDisconnection(uint delay)
		{
			int checkedDelay = checked((int)delay);

			while (true)
			{
				try
				{
					_identifier = Guid.NewGuid().ToString().Substring(0, 5);
					SendCommand(Constants.CHECK_STR + _identifier);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.StackTrace);
					Console.WriteLine(ex.Message);
					Dispose();
					OnDisconnected();
					return;
				}

				await Task.Delay(checkedDelay);
			}
		}
	}
}