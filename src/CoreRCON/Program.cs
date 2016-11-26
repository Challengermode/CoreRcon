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
				var rcon = new RCON(IPAddress.Parse("192.168.1.8"), 27015, "rcon");
				var log = new LogReceiver(IPAddress.Parse("192.168.1.8"), 0);

				// Tell the server to send logs here
				await rcon.SendCommandAsync($"logaddress_add 192.168.1.8:{log.ResolvedPort}");

				rcon.OnDisconnected += () =>
				{
					Console.WriteLine("Server closed connection!");
				};

				var status = await rcon.SendCommandAsync<Status>("status");
				Console.WriteLine($"Got status, hostname is: {status.Hostname}");

				// Listen for chat messages
				log.Listen<ChatMessage>(chat =>
				{
					Console.WriteLine($"Chat message: {chat.Player.Name} said {chat.Message} on channel {chat.Channel}");
				});

				// Listen for kills
				log.Listen<KillFeed>(kill =>
				{
					Console.WriteLine($"Player {kill.Killer.Name} ({kill.Killer.Team}) killed {kill.Killed.Name} ({kill.Killed.Team}) with {kill.Weapon}");
				});

				log.Listen<PlayerConnected>(connection =>
				{
					Console.WriteLine($"Player {connection.Player.Name} connected with host {connection.Host}");
				});

				log.Listen<PlayerDisconnected>(dis =>
				{
					Console.WriteLine($"Player {dis.Player.Name} disconnected");
				});

				log.Listen<NameChange>(name =>
				{
					Console.WriteLine($"{name.Player.Name} changed name to {name.NewName}");
				});

				// Never stop
				await Task.Delay(-1);
			});

			// .Wait() puts exceptions into an AggregateException, while .GetResult() doesn't
			task.GetAwaiter().GetResult();
		}
	}
}