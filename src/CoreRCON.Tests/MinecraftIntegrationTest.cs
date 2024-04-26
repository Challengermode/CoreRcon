/*
 Integration test that starts a Minecraft server using testcontainers and sends commands to it.
*/
using System.Threading.Tasks;
using CoreRCON.PacketFormats;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace CoreRCON.Tests;

[Trait("Type", "Integration")]  
public class MinecraftIntegrationTest(MincraftServerFixture serverFixture, ITestOutputHelper output) : IClassFixture<MincraftServerFixture>
{
    [Fact]
    public async Task TestConnectShouldConnectAndAuthenticate()
    {
        using var logger = output.BuildLoggerFor<RCON>();
        using RCON rcon = new RCON(serverFixture.RconEndpoint, serverFixture.RconPassword, logger: logger);

        await rcon.ConnectAsync();

        Assert.True(rcon.Connected);
        Assert.True(rcon.Authenticated);
    }

    [Fact]
    public async Task TestInvalidCommandShouldEchoBack()
    {
        using var logger = output.BuildLoggerFor<RCON>();
        using RCON rcon = new RCON(serverFixture.RconEndpoint, serverFixture.RconPassword, logger: logger);

        await rcon.ConnectAsync();

        string response = await rcon.SendCommandAsync("invalid");

        Assert.Contains("invalid", response);
    }

    [Fact]
    public async Task TestConcurrentRequests()
    {
        using var logger = output.BuildLoggerFor<RCON>();
        using RCON rcon = new RCON(serverFixture.RconEndpoint, serverFixture.RconPassword, logger: logger);

        await rcon.ConnectAsync();

        Task<string> task1 = rcon.SendCommandAsync("command1");
        Task<string> task2 = rcon.SendCommandAsync("command2");

        await Task.WhenAll(task1, task2);

        Assert.Contains("command1", task1.Result);
        Assert.Contains("command2", task2.Result);
    }

    [Fact]
    public async Task TestListCommandShouldReturn()
    {
        using var logger = output.BuildLoggerFor<RCON>();
        using RCON rcon = new RCON(serverFixture.RconEndpoint, serverFixture.RconPassword, logger: logger);

        await rcon.ConnectAsync();

        string response = await rcon.SendCommandAsync("list");

        Assert.Contains("There are 0 of a max of 20 players online", response);
    }

    [Fact]
    public async Task TestInfoQueryShouldReturn()
    {
        MinecraftQueryInfo serverInfo = (MinecraftQueryInfo) await ServerQuery
            .Info(serverFixture.QueryEndpoint, ServerQuery.ServerType.Minecraft);

        Assert.Equal("MINECRAFT", serverInfo.GameId);
        Assert.Equal("0", serverInfo.NumPlayers);
    }
}
