using CoreRCON.PacketFormats;
using CoreRCON.Parsers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CoreRCON
{
	public partial class RCON : IDisposable
	{
		// Since packets containing the special check string are thrown out, add some randomness to it so users can't add the check string to their name and fly under-the-radar.
		private static string _identifier;

		// When generating the packet ID, use a never-been-used (for automatic packets) ID.
		private static int _packetId = 1;

		// Allows us to keep track of when authentication succeeds, so we can block Connect from returning until it does.
		private TaskCompletionSource<bool> _authenticationTask;

		private bool _connected = false;
		private IPAddress _host;
		private string _password;
		private ushort _port;
		private uint _reconnectDelay;
		private Socket _tcp { get; set; }

		// Map of pending command references.  These are called when a command with the matching Id (key) is received.  Commands are called only once.
		private Dictionary<int, Action<string>> _pendingCommands { get; } = new Dictionary<int, Action<string>>();

		public event Action OnDisconnected;

		/// <summary>
		/// Initialize an RCON connection and automatically call ConnectAsync().
		/// </summary>
		public RCON(IPAddress host, ushort port, string password, uint reconnectDelay = 30000)
		{
			_host = host;
			_port = port;
			_password = password;
			_reconnectDelay = reconnectDelay;
			ConnectAsync().Wait();
		}

		/// <summary>
		/// Connect to a server through RCON.  Automatically sends the authentication packet.
		/// </summary>
		/// <returns>Awaitable which will complete when a successful connection is made and authentication is successful.</returns>
		public async Task ConnectAsync()
		{
			_tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			await _tcp.ConnectAsync(_host, _port);
			_connected = true;

			// Set up TCP listener
			var e = new SocketAsyncEventArgs();
			e.Completed += TCPPacketReceived;
			e.SetBuffer(new byte[Constants.MAX_PACKET_SIZE], 0, Constants.MAX_PACKET_SIZE);

			// Start listening for responses
			_tcp.ReceiveAsync(e);

			// Wait for successful authentication
			_authenticationTask = new TaskCompletionSource<bool>();
			await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _password));
			await _authenticationTask.Task;

			Task.Run(() => WatchForDisconnection(_reconnectDelay)).Forget();
		}

		public void Dispose()
		{
			_connected = false;
			_tcp.Shutdown(SocketShutdown.Both);
			_tcp.Dispose();
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.  Expect the result to be parseable into T.
		/// </summary>
		/// <typeparam name="T">Type to parse the command as.</typeparam>
		/// <param name="command">Command to send to the server.</param>
		public async Task<T> SendCommandAsync<T>(string command)
			where T : class, IParseable, new()
		{
			var source = new TaskCompletionSource<T>();
			var instance = ParserHelpers.GetParser<T>();

			var container = new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => source.SetResult((T)parsed)
			};

			_pendingCommands.Add(++_packetId, container.TryCallback);
			await SendPacketAsync(new RCONPacket(_packetId, PacketType.ExecCommand, command));
			return await source.Task;
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.  R
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		public async Task<string> SendCommandAsync(string command)
		{
			var source = new TaskCompletionSource<string>();
			_pendingCommands.Add(++_packetId, source.SetResult);
			await SendPacketAsync(new RCONPacket(_packetId, PacketType.ExecCommand, command));
			return await source.Task;
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
		}

		/// <summary>
		/// Send a packet to the server.
		/// </summary>
		/// <param name="packet">Packet to send, which will be serialized.</param>
		private async Task SendPacketAsync(RCONPacket packet)
		{
			if (!_connected) throw new InvalidOperationException("Connection is closed.");
			await _tcp.SendAsync(new ArraySegment<byte>(packet.ToBytes()), SocketFlags.None);
			//await Task.Delay(100);
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
					await SendCommandAsync(Constants.CHECK_STR + _identifier);
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