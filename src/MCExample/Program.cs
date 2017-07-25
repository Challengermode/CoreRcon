using System;
using CoreRCON;
using System.Threading.Tasks;
using System.Net;
using CoreRCON.PacketFormats;
using System.Collections.Generic;
using System.Reflection;

namespace MCExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(async () => await MainAsync(args)).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.WriteLine("Fetching info.\n");

            var status = await ServerQuery.Info(IPAddress.Parse("127.0.0.1"), 25565, ServerQuery.ServerType.Minecraft) as MinecraftQueryInfo;

            Console.WriteLine("Info has been fetched:");
            Console.WriteLine("---------------------------------");

            // Too lazy too type all infividual properties.
            IList<PropertyInfo> props = new List<PropertyInfo>(typeof(MinecraftQueryInfo).GetProperties());
            foreach (var prop in props)
            {
                if (prop.Name == "Players")
                {
                    Console.WriteLine("Players: ");
                    foreach (var player in (List<string>)prop.GetValue(status))
                    {
                        Console.WriteLine("- " + player);
                    }
                }
                else
                {
                    Console.WriteLine(prop.Name + ": " + prop.GetValue(status));
                }

            }

            Console.WriteLine("---------------------------------");
            Console.Read();
        }
    }
}