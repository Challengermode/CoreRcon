﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON;

/*
 * Simple Interactive RCON shell
 * 
 */

namespace RconShell
{
    class Program
    {
        static RconClient rcon;
        const int ThreadCount = 10;
        const int MessageCount = 10;
        static int completed = 0;

        public static async void ConcurrentTestAsync()
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} started");
            var context = SynchronizationContext.Current;
            if (context != null)
                Console.WriteLine($"Context {context.ToString()}");
            for (int i = 0; i < MessageCount; i++)
            {
                string response = await rcon.SendCommandAsync($"say {i}");
                if (response.EndsWith($"Console: {i}"))
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} failed on iteration {i} response = {response}");
                }
            }
            Interlocked.Increment(ref completed);
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} finished");
        }

        static async Task Main(string[] args)
        {


            string host;
            int port = 0;
            string password;

            Console.WriteLine("Enter Server Host/Ip");

            host = Console.ReadLine();

            // Split host and port
            if (host.Contains(":"))
            {
                var split = host.Split(':');
                host = split[0];
                port = int.Parse(split[1]);
            }

            // Resolve host
            var addresses = await Dns.GetHostAddressesAsync(host);

            if (port == 0)
            {
                Console.WriteLine("Enter port (default 27055))");
                port = int.Parse(Console.ReadLine() ?? "27055");
            }
            Console.WriteLine("Enter password");
            password = Console.ReadLine();

            var endpoint = new IPEndPoint(
                addresses.First(),
                port
            );

            rcon = new RconClient();
            await rcon.ConnectAsync(endpoint);
            bool connected = true;
            Console.WriteLine("Connected");

            Console.WriteLine("You can now enter commands to send to server:");
            rcon.OnDisconnected += () =>
            {
                Console.WriteLine("RCON Disconnected");
                connected = false;
            };

            while (connected)
            {
                string command = Console.ReadLine();
                if (command == "conctest")
                {
                    completed = 0;
                    List<Thread> threadList = new List<Thread>(ThreadCount);
                    for (int i = 0; i < ThreadCount; i++)
                    {
                        ThreadStart childref = new ThreadStart(ConcurrentTestAsync);
                        Thread childThread = new Thread(childref);
                        childThread.Start();
                        threadList.Add(childThread);
                    }
                    while (completed < ThreadCount)
                    {
                        await Task.Delay(1);
                    }
                    continue;
                }
                string response = await rcon.SendCommandAsync(command);
                Console.WriteLine(response);
            }
        }
    }
}
