using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Reflection;
using CoreRCON.PacketFormats;
using System.Net;
using System.Linq;

namespace CoreRCON
{
	public class RCON
	{
		#region Fields & Properties
		/// <summary>
		/// When generating the packet ID, use a never-been-used (for automatic packets) ID.
		/// </summary>
		private static int packetId = 1;

		/// <summary>
		/// Socket objects used to connect to RCON.
		/// </summary>
		private RCONClients sockets { get; set; } = new RCONClients();

		/// <summary>
		/// Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
		/// </summary>
		private Dictionary<int, Action<string>> pendingCommands { get; set; } = new Dictionary<int, Action<string>>();

		/// <summary>
		/// Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
		/// </summary>
		private TaskCompletionSource<bool> authenticated;

		// Lists of listeners to be polled whenever a packet is received.
		private List<ParserContainer> parseListeners { get; set; } = new List<ParserContainer>();
		private List<Action<string>> rawListeners { get; set; } = new List<Action<string>>();
		private List<Action<LogAddressPacket>> logListeners { get; set; } = new List<Action<LogAddressPacket>>();

		private IPAddress Host { get; set; }
		private ushort Port { get; set; }
		private string Password { get; set; }
		#endregion

		/// <summary>
		/// Connect to a server through RCON.  Automatically sends the authentication packet.
		/// </summary>
		/// <param name="host">Resolvable hostname.</param>
		/// <param name="port">Port number RCON is listening on.</param>
		/// <param name="password">RCON password.</param>
		/// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
		public async Task ConnectAsync(IPAddress host, ushort port, string password)
		{
			Host = host;
			Port = port;
			Password = password;

			if (Host == null) throw new NullReferenceException("Hostname cannot be null.");
			if (Password == null) throw new NullReferenceException("Password cannot be null (authentication will always fail).");

			sockets.Reset();
			await sockets.TCP.ConnectAsync(Host, Port);

			// Set up TCP listener
			var e = new SocketAsyncEventArgs();
			e.Completed += TCPPacketReceived;
			e.SetBuffer(new byte[Constants.MAX_PACKET_SIZE], 0, Constants.MAX_PACKET_SIZE);

			// Start listening for responses
			sockets.TCP.ReceiveAsync(e);

			// Wait for successful authentication
			authenticated = new TaskCompletionSource<bool>();
			await SendPacketAsync(new RCONPacket(0, PacketType.Auth, Password));
			await authenticated.Task;
		}

		// .NET Core on Linux doesn't support ConnectAsync with a hostname string, so we cheat and make sure it's an IP address
		public async Task ConnectAsync(string ip, ushort port, string password)
		{
			await ConnectAsync(IPAddress.Parse(ip), port, password);
		}

		/// <summary>
		/// Listens on the socket for a parseable class to read.
		/// Most useful with StartLogging(), though this will also fire when a response is received from the TCP connection.
		/// </summary>
		/// <typeparam name="T">Class to be parsed; must have a ParserAttribute.</typeparam>
		/// <param name="result">Parsed class.</param>
		public void Listen<T>(Action<T> result)
			where T : class, new()
		{
			// Instantiate the parser associated with the type parameter
			var instance = ParserHelpers.GetParser<T>();

			// Create the parser container
			parseListeners.Add(new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => result(parsed as T)
			});
		}

		/// <summary>
		/// Listens on the socket for anything, returning just the body of the packet.
		/// Most useful with StartLogging(), though this will also fire when a response is received from the TCP connection.
		/// </summary>
		/// <param name="result">Raw string returned by the server.</param>
		public void Listen(Action<string> result)
		{
			rawListeners.Add(result);
		}

		/// <summary>
		/// Listens on the socket for anything from LogAddress, returning the full packet.
		/// StartLogging() should be run with this.
		/// </summary>
		/// <param name="result">Parsed LogAddress packet.</param>
		public void Listen(Action<LogAddressPacket> result)
		{
			logListeners.Add(result);
		}

		/// <summary>
		/// Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
		/// </summary>
		/// <param name="delay">Time in milliseconds to wait between polls.</param>
		public async Task KeepAliveAsync(int delay = 30000)
		{
			while (true)
			{
				try { await SendCommandAsync(""); }
				catch (Exception)
				{
					Console.Error.WriteLine($"{DateTime.Now} - Disconnected from {sockets.TCP.RemoteEndPoint}... Attempting to reconnect.");
					await ConnectAsync(Host, Port, Password);
					return;
				}

				await Task.Delay(delay);
			}
		}

		/// <summary>
		/// Opens a socket to receive LogAddress logs, and registers it with the server.  The IP can also be a local IP if the server is on the same network.
		/// </summary>
		/// <param name="sendToIp">The IP address to tell the server where to send logs.</param>
		public async Task StartLogging(string sendToIp)
		{
			IPAddress parsed;
			if (!IPAddress.TryParse(sendToIp, out parsed)) throw new ArgumentException($"{sendToIp} is not a valid IP address.");
			if (parsed.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException($"{nameof(sendToIp)} must be an IPv4 address.");

			var udpPort = ((IPEndPoint)(sockets.UDP.Client.LocalEndPoint)).Port;

			// Add the UDP client to logaddress
			await SendCommandAsync($"logaddress_add {sendToIp}:{udpPort}");

			Task.Run(async () =>
			{
				while (true)
				{
					var result = await sockets.UDP.ReceiveAsync();

					// Parse out the LogAddress packet
					LogAddressPacket packet = LogAddressPacket.FromBytes(result.Buffer);
					LogAddressPacketReceived(packet);
				}
			}).DoNotAwait();
		}

		/// <summary>
		/// Send a command to the server, and call the result when a response is received.
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		/// <param name="result">Response from the server.</param>
		public async Task SendCommandAsync(string command, Action<string> result = null)
		{
			// Get a unique integer
			RCONPacket packet = new RCONPacket(++packetId, PacketType.ExecCommand, command);
			pendingCommands.Add(packetId, result);
			await SendPacketAsync(packet);
		}

		public async Task SendCommandAsync<T>(string command, Action<T> result)
			where T : class, new()
		{
			var instance = ParserHelpers.GetParser<T>();
			var container = new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => result(parsed as T)
			};

			RCONPacket packet = new RCONPacket(++packetId, PacketType.ExecCommand, command);
			pendingCommands.Add(packetId, container.TryCallback);
			await SendPacketAsync(packet);
		}

		public async Task<T> SendCommandAsync<T>(string command)
			where T : class, new()
		{
			var source = new TaskCompletionSource<T>();
			await SendCommandAsync<T>(command, source.SetResult);
			return await source.Task;
		}

		/// <summary>
		/// Send a packet to the server.
		/// </summary>
		/// <param name="packet">Packet to send, which will be serialized.</param>
		private async Task SendPacketAsync(RCONPacket packet)
		{
			sockets.TCP.Send(packet.ToBytes());
			await Task.Delay(10);
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
					throw new AuthenticationException($"Authentication failed for {sockets.TCP.RemoteEndPoint}.");
				}

				// Tell Connect that authentication succeeded
				authenticated.SetResult(true);
			}

			// Forward to handler
			RCONPacketReceived(packet);

			// Continue listening
			sockets.TCP.ReceiveAsync(e);
		}

		private void RCONPacketReceived(RCONPacket packet)
		{
			// Call pending result and remove from map
			Action<string> action;
			if (pendingCommands.TryGetValue(packet.Id, out action))
			{
				action?.Invoke(packet.Body);
				pendingCommands.Remove(packet.Id);
			}

			CallListeners(packet.Body);
		}

		private void LogAddressPacketReceived(LogAddressPacket packet)
		{
			// Call LogAddress listeners
			foreach (var listener in logListeners)
				listener(packet);

			// Lower priority
			CallListeners(packet.RawBody);
		}

		private void CallListeners(string body)
		{
			if (body.Length < 1) return;

			// Call parsers
			foreach (var parser in parseListeners)
				parser.TryCallback(body);

			// Call raw listeners
			foreach (var listener in rawListeners)
				listener(body);
		}
	}
}