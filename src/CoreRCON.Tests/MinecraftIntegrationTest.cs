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

    private readonly IContainer _minecraftContainer = new ContainerBuilder()
            .WithImage("itzg/minecraft-server")
            .WithEnvironment("EULA", "TRUE")
            .WithEnvironment("RCON_PASSWORD", _rconPassword)
            .WithExposedPort(25565)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(25565))
            .WithPortBinding(25565, true)
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
        IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(_minecraftContainer.IpAddress),_minecraftContainer.GetMappedPublicPort(25565));
        RCON rcon = new RCON(ipEndpoint, _rconPassword);

        await rcon.ConnectAsync();

        Assert.True(rcon.Connected);
        Assert.True(rcon.Authenticated);
    }
}
