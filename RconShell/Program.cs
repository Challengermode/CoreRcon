using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CoreRCON;
using CoreRCON.PacketFormats;

/*
 * Simple Interactive RCON shell
 * 
 */

namespace RconShell
{
    class Program
    {
        static void Main(string[] args)
        {
            var task = Task.Run(async () =>
            {
                String ip = "192.168.2.224";
                int port = 27015;
                String password = "rcon";
                var endpoint = new IPEndPoint(
                    IPAddress.Parse(ip),
                    port
                );

                var rcon = new RCON(endpoint, password);
                bool connected = true;
                rcon.OnDisconnected += () =>
                {
                    connected = false;
                };

                while (connected)
                {
                    String command = Console.ReadLine();
                    String response = await rcon.SendCommandAsync(command);
                    Console.WriteLine(response);
                }

            });

            // .Wait() puts exceptions into an AggregateException, while .GetResult() doesn't
            task.GetAwaiter().GetResult();
        }
    }
}
