/*
 Integration test that starts a Minecraft server using testcontainers and sends commands to it.
*/
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace CoreRCON.Tests;

public class MincraftServerFixture : IAsyncLifetime
{
    
    public string RconPassword;
    public IPEndPoint RconEndpoint;
    public IPEndPoint QueryEndpoint;

    private const ushort RconPort = 25575;
    private const ushort QueryPort = 25585;

    private readonly ContainerBuilder _containerBuilder = new ContainerBuilder()
            .WithImage("itzg/minecraft-server")
            .WithEnvironment("EULA", "TRUE")
            .WithEnvironment("ENABLE_RCON", "true")
            .WithEnvironment("ENABLE_QUERY", "true")
            .WithEnvironment("RCON_PORT", RconPort.ToString())
            .WithEnvironment("QUERY_PORT", QueryPort.ToString())
            .WithEnvironment("ONLINE_MODE", "FALSE")
            .WithEnvironment("VIEW_DISTANCE", "1")
            .WithEnvironment("LEVEL_TYPE", "FLAT")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(RconPort))
            .WithPortBinding(RconPort, true)
            .WithPortBinding($"{QueryPort}/udp", false);

    private IContainer _minecraftContainer;

    public async Task InitializeAsync()
    {
        /// Generate random password
        RconPassword = Guid.NewGuid().ToString("N");
        _minecraftContainer = _containerBuilder
            .WithEnvironment("RCON_PASSWORD", RconPassword)
            .Build();

        await _minecraftContainer.StartAsync();

        IPAddress containerIp = (await Dns.GetHostAddressesAsync(_minecraftContainer.Hostname)).First();
        RconEndpoint = new IPEndPoint(containerIp, _minecraftContainer.GetMappedPublicPort(RconPort));
        QueryEndpoint = new IPEndPoint(containerIp, QueryPort);
    }

    public Task DisposeAsync()
    {
        return _minecraftContainer.DisposeAsync().AsTask();
    }
}
