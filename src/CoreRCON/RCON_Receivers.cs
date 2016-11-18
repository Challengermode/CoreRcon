using CoreRCON.PacketFormats;
using System;
using System.Net.Sockets;

namespace CoreRCON
{
	public partial class RCON
	{
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
			_tcp.ReceiveAsync(e);
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

		private void LogAddressPacketReceived(LogAddressPacket packet)
		{
			// Filter out checks
			if (packet.Body.Contains(Constants.CHECK_STR + _identifier)) return;

			// Call LogAddress listeners
			foreach (var listener in _logListeners)
				listener(packet);

			// Lower priority
			CallListeners(packet.RawBody);
		}

		private void CallListeners(string body)
		{
			if (body.Length < 1) return;

			// Call parsers
			foreach (var parser in _parseListeners)
				parser.TryCallback(body);

			// Call raw listeners
			foreach (var listener in _rawListeners)
				listener(body);
		}
	}
}
