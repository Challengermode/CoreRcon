using CoreRCON.PacketFormats;
using CoreRCON.Parsers.Standard;
using System;
using System.Net;
using System.Threading.Tasks;

namespace CoreRCON
{
	internal class ExampleProgram
	{
		/// <summary>
		/// Example program for CoreRCON.
		/// </summary>
		internal static void Main(string[] args)
		{
			var task = Task.Run(async () =>
			{
				var rcon = new RCON(new RCONOptions
				{
					ServerHost = IPAddress.Parse("192.168.1.8"),
					ServerPort = 27015,
					Password = "rcon",
					EnableLogging = true,
					LogHost = IPAddress.Parse("192.168.1.8"),
					LogPort = 56180,
					DisconnectionCheckInterval = 1000
				});

				await rcon.ConnectAsync();

				rcon.SendCommand("echo test", test =>
				{
					Console.WriteLine(test);
				});

				rcon.SendCommand("echo test2", test2 =>
				{
					Console.WriteLine(test2);
				});

				rcon.OnDisconnected += () =>
				{
					Console.WriteLine("Server closed connection!");
				};

				// Listen for chat messages
				rcon.Listen<ChatMessage>(chat =>
				{
					Console.WriteLine($"Chat message: {chat.Player.Name} said {chat.Message} on channel {chat.Channel}");
				});

				// Listen for kills
				rcon.Listen<KillFeed>(kill =>
				{
					Console.WriteLine($"Player {kill.Killer.Name} ({kill.Killer.Team}) killed {kill.Killed.Name} ({kill.Killed.Team}) with {kill.Weapon}");
				});

				rcon.Listen<PlayerConnected>(connection =>
				{
					Console.WriteLine($"Player {connection.Player.Name} connected with host {connection.Host}");
				});

				rcon.Listen<PlayerDisconnected>(dis =>
				{
					Console.WriteLine($"Player {dis.Player.Name} disconnected");
				});

				rcon.Listen<NameChange>(name =>
				{
					Console.WriteLine($"{name.Player.Name} changed name to {name.NewName}");
				});

				await Task.Delay(-1);
			});

			// .Wait() puts exceptions into an AggregateException, while .GetResult() doesn't
			task.GetAwaiter().GetResult();
		}
	}
}