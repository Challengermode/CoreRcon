using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Polly;
using System.Net;
using Polly.Retry;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Net.Http.Json;
using Xunit.Abstractions;
using Neovolve.Logging.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Collections.Generic;


namespace CoreRCON.Tests;

public class CsServerFixture : IAsyncLifetime
{
    // RCON password for the server used in tests
    internal readonly string _rconPassword;
    // IP and port of the RCON server used in tests
    internal IPEndPoint _rconEndpoint;

    private string _serverId;
    private readonly HttpClient _client;
    private readonly AsyncRetryPolicy _retryPolicy;

    public CsServerFixture()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets<CsServerFixture>()
            .AddEnvironmentVariables(prefix:"APPSECRETS_")            
            .Build();
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://dathost.net/api/0.1/game-servers/"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        string authString = Convert.ToBase64String(Encoding.ASCII.GetBytes(configuration["DATHOST_API_TOKEN"]));
        _client.DefaultRequestHeaders.Add("Authorization", "Basic " + authString);
        // random password for the RCON server
        _rconPassword = Guid.NewGuid().ToString("N").Substring(0, 10);

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task InitializeAsync()
    {
        await _retryPolicy.ExecuteAsync(CreateServer);
        await _retryPolicy.ExecuteAsync(StartServer);
    }

    public Task DisposeAsync()
    {
        // Stop and delete the server
        return _retryPolicy.ExecuteAsync(async () =>
        {
            await StopServer();
            await DeleteServer();
            Console.WriteLine("Server stopped and deleted");
        });
    }

    public async Task StopServer()
    {
        HttpResponseMessage stopResponse = await _client.PostAsync($"{_serverId}/stop", null);
        stopResponse.EnsureSuccessStatusCode();
    }

    public RCON GetRconClient(ITestOutputHelper output = null)
    {
        ICacheLogger<RCON> rconLogger = output?.BuildLoggerFor<RCON>();
        return new RCON(_rconEndpoint, _rconPassword, logger: rconLogger);
    }

    private async Task StartServer()
    {
        HttpResponseMessage startResponse = await _client.PostAsync($"{_serverId}/start", null);
        startResponse.EnsureSuccessStatusCode();

        var returnType = new { raw_ip = "", ports = new { game = 1337 } };

        // Get server IP and port
        HttpResponseMessage serverInfoResponse = await _client.GetAsync($"{_serverId}");
        serverInfoResponse.EnsureSuccessStatusCode();

        dynamic serverInfo = await serverInfoResponse.Content.ReadFromJsonAsync(returnType.GetType());

        if (serverInfo.raw_ip == null)
        {
            throw new Exception("Failed to get server info");
        }

        _rconEndpoint = new IPEndPoint(IPAddress.Parse(serverInfo.raw_ip), serverInfo.ports.game);
        Console.WriteLine("Server started");
    }

    private async Task DeleteServer()
    {
        HttpResponseMessage delete = await _client.DeleteAsync($"{_serverId}");
        delete.EnsureSuccessStatusCode();
        Console.WriteLine("Server stopped and deleted sucessfully");
    }

    private async Task CreateServer()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "name", "CoreRCON integration test server" },
                { "game", "cs2" },
                { "location", "dusseldorf" },
                { "cs2_settings.slots", "5" },
                { "cs2_settings.rcon", _rconPassword },
            })
        };

        HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var server = new { id = "" };
        // read response as JSON into anonymous type with serverID
        dynamic responseContent = await response.Content.ReadFromJsonAsync(server.GetType());
        _serverId = responseContent.id;

        Console.WriteLine($"Created server with ID {_serverId}");
    }
}
