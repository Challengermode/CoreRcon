using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using CoreRCON;
using System.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

/*
 * Run tests against a running RCON server
 * How to run:
 * Start a server (preferably csgo)
 * set the cvar "log off" on the server
 * configure the properties bellow
 * run the tests
 *
 * Todo: Automate testing
 * Todo: Make sure the tests work with log on
 */

namespace CoreRCON.Tests
{
    [TestClass]
    public class RconTest
    {

        RCON rconClient;
        //Connection settings for server
        private readonly IPAddress _ip = IPAddress.Parse("127.0.0.1");
        private readonly ushort _port = 27015;
        private readonly string _password = "rcon";

        [TestCleanup]
        public void testClean()
        {
            rconClient.Dispose();
        }

        [TestInitialize]
        public async Task testInitAsync()
        {
            rconClient = new RCON(_ip, _port, _password, 1000, false);
            await rconClient.ConnectAsync();

        }


        [TestMethod]
        [ExpectedException(typeof(AuthenticationException))]
        public async Task testBadAuthAsync()
        {
            //Warning ! This test can ban your ip in the server if sv_rcon_maxfailure is set to 0
            //Use removeip to unban your ip (Default ban period is 60 min)
            rconClient.Dispose();
            rconClient = new RCON(_ip, _port, "wrong PW");
            await rconClient.ConnectAsync();
        }

        [TestMethod]
        public async Task testEmptyResponseAsync()
        {
            string response = await rconClient.SendCommandAsync("//comment");
            Assert.AreEqual("", response);
        }

        [TestMethod]
        public async Task testEchoAsync()
        {
            string response = await rconClient.SendCommandAsync("say hi");
            Assert.AreEqual("Console: hi", response);
        }


        [TestMethod]
        public async Task testLongResponseAsync()
        {
            rconClient.Dispose();
            rconClient = new RCON(_ip, _port, _password, 10000, true); //Enable multi packetsupport
            await rconClient.ConnectAsync();
            string response = await rconClient.SendCommandAsync("cvarList");
            Assert.IsTrue(response.EndsWith("total convars/concommands"));
        }


        [TestMethod]
        public async Task testMultipleCommands()
        {
            for (int i = 0; i < 10; i++)
            {
                string response = await rconClient.SendCommandAsync($"say {i}");
                Assert.AreEqual($"Console: {i}", response);
            }
        }

        [TestMethod]
        public async Task testCommandsConcurent()
        {
            List<Task> tasks = new List<Task>();

            tasks = Enumerable.Range(1, 10)
                .Select(async (i) =>
                {
                    string response = await rconClient.SendCommandAsync($"say {i}");
                    Console.WriteLine($"recived response {i} : {response}");
                    Assert.AreEqual($"Console: {i}", response);
                }).ToList();
            //Parallel.ForEach(tasks, task => task.Start());
            await Task.WhenAll(tasks);
            Console.Out.Flush();
        }



        [TestMethod, Timeout(30000)]
        [ExpectedException(typeof(SocketException))]
        public async Task testNetworkCut()
        {
            //1. Put a brakepoint on the line bellow
            //2. When the debugger breaks quickly unplug the ethernet 
            //3. Continue
            // Todo: Find a way to simulate this using software
            string response = await rconClient.SendCommandAsync("say hi");
        }

        [TestMethod]
        public async Task testUnicode()
        {
            string unicodeString = "יוהצ";
            string response = await rconClient.SendCommandAsync($"say {unicodeString}");
            Assert.IsTrue(response.Contains(unicodeString));
        }
    }
}
