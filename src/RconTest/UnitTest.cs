using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using RCONServerLib;
using CoreRCON;
using System.Net;

namespace CoreRCON.Tests
{
    [TestClass]
    public class UnitTest
    {

        RemoteConServer rconServer;
        RCON rconClient;

        [TestCleanup]
        public void testClean()
        {
            rconClient.Dispose();
            //rconServer.StopListening();
        }

        [TestInitialize]
        public async Task testInitAsync()
        {
            rconServer = new RemoteConServer(IPAddress.Loopback, 27015);
            rconServer.EmptyPayloadKick = false; //Allow multi packet test command through
            rconServer.InvalidPacketKick = false;
            rconServer.Password = "rconpasswordtest";
            rconServer.StartListening();
            rconClient = new RCON(IPAddress.Loopback, 27015, rconServer.Password);
            await rconClient.ConnectAsync();

        }


        [TestMethod]
        public void testBadAuthAsync()
        {
            rconClient.Dispose();
            rconClient = new RCON(IPAddress.Loopback, 27015, "wrong PW");
            Assert.ThrowsException<AuthenticationException>(() =>
            {
                rconClient.ConnectAsync().Wait();
            });
        }

        [TestMethod]
        public async Task testEmptyResponse()
        {
            rconServer.CommandManager.Add("Empty", "", (command, arguments) =>
            {
                return "";
            });
            string response = await rconClient.SendCommandAsync("Empty");
            Assert.AreEqual("", response);
        }
    }
}
