using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            bool autoConnect = true;
            string host;
            int port = 0;
            string password;

            Console.WriteLine("Enter Server Host/Ip (Default 127.0.0.1)");

            var input = Console.ReadLine();
            host = string.IsNullOrWhiteSpace(input) ? "127.0.0.1" : input;

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
                Console.WriteLine("Enter port (default 27055)");
                input = Console.ReadLine();
                port = int.Parse( string.IsNullOrWhiteSpace(input) ? "27055" : input);
            }
            Console.WriteLine("Enter password");
            password = Console.ReadLine();

            var endpoint = new IPEndPoint(
                addresses.First(),
                port
            );
            bool connected = false;
            
            rcon = new RCON(endpoint, password, 0, strictCommandPacketIdMatching: false, autoConnect: autoConnect);
            rcon.OnDisconnected += () =>
            {
                Console.WriteLine("RCON Disconnected");
                connected = false;
            };
            
            var tryConnect = true;
            do
            {
                try
                {
                    await rcon.ConnectAsync();
                    connected = true;
                    Console.WriteLine($"Connected ({endpoint})");
                    Console.WriteLine("You can now enter commands to send to server:");
                    while (connected || autoConnect)
                    {
                        string command = Console.ReadLine();
                        if (!connected && !autoConnect)
                            break;
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

                        try
                        {
                            string response = await rcon.SendCommandAsync(command);
                            Console.WriteLine(response);
                        }
                        catch (ArgumentException ex)
                        {
                            var prevColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(ex.Message);
                            Console.ForegroundColor = prevColor;
                        }
                    }
                }
                catch (AuthenticationFailedException ex)
                {
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Authentication failed: Invalid password");
                    Console.ForegroundColor = prevColor;
                    Console.WriteLine("Enter password");
                    password = Console.ReadLine();
                    rcon.SetPassword(password);
                    continue;
                }
                catch (Exception e)
                {
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ForegroundColor = prevColor;
                    
                }
                
                while (true)
                {
                    Console.WriteLine("Attempt to reconnect? (y/n)");
                    var retry = Console.ReadLine();
                    if (retry.ToLower() == "y")
                        break;

                    if (retry.ToLower() == "y")
                    {
                        tryConnect = false;
                        break;
                    }

                    Console.WriteLine("Invalid input.");
                }

            } while (tryConnect);

        }
    }
}
