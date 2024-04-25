/*
 Integration test that starts a Minecraft server using testcontainers and sends commands to it.
*/
using System.Net;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace CoreRCON.Tests;

public class MinecraftIntegrationTest : IAsyncLifetime
{

    private const string _rconPassword = "test123";
    private const ushort _rconPort = 25575;

    private readonly IContainer _minecraftContainer = new ContainerBuilder()
            .WithImage("itzg/minecraft-server")
            .WithEnvironment("EULA", "TRUE")
            .WithEnvironment("ENABLE_RCON", "true")
            .WithEnvironment("RCON_PORT", _rconPort.ToString())
            .WithEnvironment("RCON_PASSWORD", _rconPassword)
            .WithEnvironment("ONLINE_MODE", "FALSE")
            .WithEnvironment("VIEW_DISTANCE", "1")
            .WithEnvironment("LEVEL_TYPE", "FLAT")
            .WithExposedPort(_rconPort)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(_rconPort))
            .WithPortBinding(_rconPort, true)
            .Build();


    public Task InitializeAsync()
    {
        return _minecraftContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _minecraftContainer.DisposeAsync().AsTask();
    }


    [Fact]
    public async Task TestMinecraftServer()
    {
        IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(_minecraftContainer.Hostname), _minecraftContainer.GetMappedPublicPort(_rconPort));
        RCON rcon = new RCON(ipEndpoint, _rconPassword);

        await rcon.ConnectAsync();

        Assert.True(rcon.Connected);
        Assert.True(rcon.Authenticated);
    }
}
