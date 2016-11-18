using CoreRCON.Parsers;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using CoreRCON.PacketFormats;
using System.Diagnostics;
using System.Threading;

namespace CoreRCON
{
	public partial class RCON : IDisposable
	{
		public RCON(RCONOptions options)
		{
			if (options.ServerHost == null) throw new NullReferenceException("Server hostname cannot be null.");
			if (options.Password == null) throw new NullReferenceException("Password cannot be null (authentication will always fail).");

			_options = options;
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
			_rawListeners.Add(result);
		}

		/// <summary>
		/// Listens on the socket for anything from LogAddress, returning the full packet.
		/// StartLogging() should be run with this.
		/// </summary>
		/// <param name="result">Parsed LogAddress packet.</param>
		public void Listen(Action<LogAddressPacket> result)
		{
			_logListeners.Add(result);
		}

		/// <summary>
		/// Send a command to the server, and call the result when a response is received.
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		/// <param name="result">Response from the server.</param>
		public void SendCommandAsync(string command, Action<string> result)
		{
			RCONPacket packet = new RCONPacket(++_packetId, PacketType.ExecCommand, command);
			_pendingCommands.Add(_packetId, result);
			SendPacketAsync(packet).GetAwaiter().GetResult();
		}

		/// <summary>
		/// Send a command to the server, and call the result when a response is received.
		/// </summary>
		/// <typeparam name="T">Type to parse the command as.</typeparam>
		/// <param name="command">Command to send to the server.</param>
		/// <param name="result">Strongly typed response from the server.</param>
		public void SendCommandAsync<T>(string command, Action<T> result)
			where T : class, IParseable, new()
		{
			var instance = ParserHelpers.GetParser<T>();
			var container = new ParserContainer
			{
				IsMatch = line => instance.IsMatch(line),
				Parse = line => instance.Parse(line),
				Callback = parsed => result(parsed as T)
			};

			RCONPacket packet = new RCONPacket(++_packetId, PacketType.ExecCommand, command);
			_pendingCommands.Add(_packetId, container.TryCallback);
			SendPacketAsync(packet).GetAwaiter().GetResult();
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
			SendCommandAsync<T>(command, source.SetResult);
			return await source.Task;
		}

		/// <summary>
		/// Send a command to the server, and wait for the response before proceeding.
		/// </summary>
		/// <param name="command">Command to send to the server.</param>
		public async Task<string> SendCommandAsync(string command)
		{
			var source = new TaskCompletionSource<string>();
			SendCommandAsync(command, source.SetResult);
			return await source.Task;
		}

		/// <summary>
		/// Send a packet to the server.
		/// </summary>
		/// <param name="packet">Packet to send, which will be serialized.</param>
		private async Task SendPacketAsync(RCONPacket packet)
		{
			if (!_connected) throw new InvalidOperationException("Connection is closed.");
			_tcp.Send(packet.ToBytes());
			await Task.Delay(100);
		}

		/// <summary>
		/// Reset the mostly-unique-enough per-request identifier.
		/// </summary>
		private static void ResetIdentifier()
		{
			_identifier = Guid.NewGuid().ToString().Substring(0, 5);
		}

		public void Dispose()
		{
			_connected = false;
			_tcp.Shutdown(SocketShutdown.Both);
			_tcp.Dispose();
			_udp.Dispose();
		}
	}
}