using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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
        static RCON rcon;
        const int ThreadCount = 10;
        const int MessageCount = 10;
        static int completed = 0;

        public static async void ConcurrentTestAsync()
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} started");
            var context = SynchronizationContext.Current;
            if (context != null)
                Console.WriteLine($"Context {context.ToString()}");
            for (int i = 0; i< MessageCount; i++)
            {
                string response = await rcon.SendCommandAsync($"say {i}");
                if(response.EndsWith($"Console: {i}"))
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} failed on iteration {i} response = {response}");
                }
            }
            Interlocked.Increment(ref completed);
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} finished");
        }

        static async Task Main(string[] args)
        {
            String ip;
            int port;
            String password;

            Console.WriteLine("Enter ip");
            ip = Console.ReadLine();
            Console.WriteLine("Enter port");
            port = int.Parse(Console.ReadLine());
            Console.WriteLine("Enter password");
            password = Console.ReadLine();

            var endpoint = new IPEndPoint(
                IPAddress.Parse(ip),
                port
            );

            rcon = new RCON(endpoint, password, 0);
            await rcon.ConnectAsync();
            bool connected = true;
            Console.WriteLine("Connected");

            rcon.OnDisconnected += () =>
            {
                Console.WriteLine("RCON Disconnected");
                connected = false;
            };

            while (connected)
            {
                String command = Console.ReadLine();
                if(command == "conctest")
                {
                    completed = 0;
                    List<Thread> threadList = new List<Thread>(ThreadCount);
                    for (int i = 0; i<ThreadCount; i++)
                    {
                        ThreadStart childref = new ThreadStart(ConcurrentTestAsync);
                        Thread childThread = new Thread(childref);
                        childThread.Start();
                        threadList.Add(childThread);
                    }
                    while(completed < ThreadCount)
                    {
                        await Task.Delay(1);
                    }
                    continue;
                }
                String response = await rcon.SendCommandAsync(command);
                Console.WriteLine(response);
            }
        }
    }
}
