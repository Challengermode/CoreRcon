using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

namespace CoreRCON.Tests;

[TestClass]
public class RconTest
{
    private RconClient rconClient;
    //Connection settings for server
    private readonly IPAddress _ip = IPAddress.Parse("212.102.62.201");
    private readonly ushort _port = 26795;
    private readonly string _password = "o8A2W6I6C5I1h0";

    [TestCleanup]
    public void testClean()
    {
        rconClient?.Dispose();
    }

    [TestInitialize]
    public async Task testInitAsync()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("ConsoleApp", LogLevel.Debug)
                .AddConsole();
        });
        ILogger<RconClient> logger = loggerFactory.CreateLogger<RconClient>();

        rconClient = new RconClient(5000);
        await rconClient.ConnectAsync(new IPEndPoint(_ip, _port));
        bool authenticated = await rconClient.AuthenticateAsync(_password);
    }

    [TestMethod]
    public async Task testBadAuthAsync()
    {
        //Warning ! This test can ban your ip in the server if sv_rcon_maxfailure is set to 0
        //Use removeip to unban your ip (Default ban period is 60 min)

        bool authenticated = await rconClient.AuthenticateAsync("wrong pw");
        Assert.IsFalse(authenticated);
    }

    [TestMethod]
    public async Task testGoodAuthAsync()
    {
        //Warning ! This test can ban your ip in the server if sv_rcon_maxfailure is set to 0
        //Use removeip to unban your ip (Default ban period is 60 min)
        bool authenticated = await rconClient.AuthenticateAsync(_password);
        Assert.IsTrue(authenticated);
    }

    [TestMethod]
    public async Task testEmptyResponseAsync()
    {
        bool authenticated = await rconClient.AuthenticateAsync(_password);
        string response = await rconClient.SendCommandAsync("//comment");
        Assert.AreEqual("", response);
    }

    [TestMethod]
    public async Task testEchoAsync()
    {
        bool authenticated = await rconClient.AuthenticateAsync(_password);
        string response = await rconClient.SendCommandAsync("say hi");
        Assert.IsTrue(response.Contains("hi"));
    }

    [TestMethod]
    public async Task testEchoMultipacketAsync()
    {
        bool authenticated = await rconClient.AuthenticateAsync(_password);
        string response = await rconClient.SendCommandAsync("say hi", multipacket: true);
        Assert.IsTrue(response.Contains("hi"));
    }

    [TestMethod]
    public async Task testLongResponseAsync()
    {
        string response = await rconClient.SendCommandAsync("cvarList", multipacket: true);
        string lastPart = response.Substring(response.Length - 30);
        Console.WriteLine($"received response: {lastPart}, total length: {response.Length}");
        Assert.IsTrue(response.EndsWith("total convars/concommands\n"));
    }


    [TestMethod]
    public async Task testMultipleCommands()
    {
        for (int i = 0; i < 10; i++)
        {
            string response = await rconClient.SendCommandAsync($"say {i}");
            Assert.IsTrue(response.Contains($"{i}"));
        }
    }


    [TestMethod]
    public async Task testPacketsWrongOrder()
    {

        for (int i = 0; i < 300; i++)
        {
            Console.WriteLine($"Iteration: {i}");
            await Task.Delay(50);

            string response1 = await rconClient.SendCommandAsync($"status", true);
            string response2 = await rconClient.SendCommandAsync($"status_json", true);
            string response3 = await rconClient.SendCommandAsync($"mp_backup_restore_list_files", true);
            bool response1ContainsServer = response1.Length == 0 || response1.Contains("Server");
            bool response2ContainsFrametime = response2.Length == 0 || response2.Contains("frametime");
            bool response3ContainsUnknown = response3.Length == 0 || response3.Contains("Listing backup files with prefix");
            if (!response1ContainsServer || !response2ContainsFrametime || !response3ContainsUnknown)
            {
                Console.WriteLine($"received response1: {response1}");
                Console.WriteLine($"received response2: {response2}");
                Console.WriteLine($"received response3: {response3}");
            }
            Assert.IsTrue(response1ContainsServer);
            Assert.IsTrue(response2ContainsFrametime);
            Assert.IsTrue(response2ContainsFrametime);

        }
    }

    [TestMethod]
    public async Task testCommandsConcurent()
    {

        await rconClient.AuthenticateAsync(_password);
        List<Task> tasks = new List<Task>();
        tasks = Enumerable.Range(1, 10)
            .Select(async (i) =>
            {
                string response = await rconClient.SendCommandAsync($"say {i}", false);
                Console.WriteLine($"received response {i} : {response}");
                Assert.IsTrue(response.Contains($"{i}"));
            }).ToList();
        //Parallel.ForEach(tasks, task => task.Start());
        await Task.WhenAll(tasks);
        Console.Out.Flush();
    }

    [TestMethod, Timeout(30000)]
    [ExpectedException(typeof(SocketException))]
    public async Task testNetworkCut()
    {
        bool authenticated = await rconClient.AuthenticateAsync(_password);
        if (authenticated)
        {
            //1. Put a brakepoint on the line bellow
            //2. When the debugger breaks quickly unplug the ethernet 
            //3. Continue
            // Todo: Find a way to simulate this using software
            string response = await rconClient.SendCommandAsync("say hi");
        }
    }

    [TestMethod]
    public async Task testUnicode()
    {
        rconClient = new RconClient();
        await rconClient.ConnectAsync(new IPEndPoint(_ip, _port));
        bool authenticated = await rconClient.AuthenticateAsync(_password);
        string unicodeString = "éåäö";
        string response = await rconClient.SendCommandAsync($"say {unicodeString}");
        Assert.IsTrue(response.Contains(unicodeString));
    }
}

