using CoreRCON.PacketFormats;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CoreRCON
{
	public partial class RCON
	{
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
			e.SetBuffer(new byte[Constants.MAX_PACKET_SIZE], 0, Constants.MAX_PACKET_SIZE);

			// Start listening for responses
			_tcp.ReceiveAsync(e);

			// Wait for successful authentication
			_authenticationTask = new TaskCompletionSource<bool>();
			await SendPacketAsync(new RCONPacket(0, PacketType.Auth, _options.Password));
			await _authenticationTask.Task;

			Task.Run(() => WatchForDisconnection(_options.DisconnectionCheckInterval)).DoNotAwait();

			if (_options.EnableLogging)
			{
				Task.Run(() => StartLogging()).DoNotAwait();
			}
		}

		/// <summary>
		/// Opens a socket to receive LogAddress logs, and registers it with the server.  The IP can also be a local IP if the server is on the same network.
		/// </summary>
		private async Task StartLogging()
		{
			_udp = new UdpClient(_options.LogPort);
			var resolvedPort = ((IPEndPoint)(_udp.Client.LocalEndPoint)).Port;

			// Add the UDP client to logaddress
			await SendCommandAsync($"logaddress_add {_options.LogHost}:{resolvedPort}");

			while (true)
			{
				var result = await _udp.ReceiveAsync();

				// Parse out the LogAddress packet
				LogAddressPacket packet = LogAddressPacket.FromBytes(result.Buffer);
				LogAddressPacketReceived(packet);
			}
		}

		/// <summary>
		/// Polls the server to check if RCON is still authenticated.  Will still throw if the password was changed elsewhere.
		/// </summary>
		/// <param name="delay">Time in milliseconds to wait between polls.</param>
		private async Task WatchForDisconnection(uint delay)
		{
			int checkedDelay = checked((int)delay);

			while (true)
			{
				try
				{
					ResetIdentifier();
					await SendCommandAsync(Constants.CHECK_STR + _identifier);
				}
				catch
				{
					Console.Error.WriteLine($"{DateTime.Now} - Disconnected from {_tcp.RemoteEndPoint}... Attempting to reconnect.");
					Dispose();
					OnDisconnected();
					return;
				}

				await Task.Delay(checkedDelay);
			}
		}
	}
}