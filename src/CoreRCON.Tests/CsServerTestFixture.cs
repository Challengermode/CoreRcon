using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Polly;
using System.Net;
using Polly.Retry;
using Microsoft.Extensions.Configuration;
using System.Text;


public class CsServerFixture : IAsyncLifetime
{
    internal readonly string _rconPassword;

    internal readonly IPEndPoint _rconEndpoint;

    private readonly HttpClient _client;

    private string _serverId;

    private readonly AsyncRetryPolicy _retryPolicy;

    public CsServerFixture(IConfiguration configuration)
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://dathost.net/api/0.1/"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        string authString = Convert.ToBase64String(Encoding.ASCII.GetBytes(configuration["DATHOST_API_KEY"]));
        _client.DefaultRequestHeaders.Add("Authorization", "Basic " + authString);
        // random password for the RCON server
        _rconPassword = Guid.NewGuid().ToString("N");

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public Task InitializeAsync()
    {
        // Start the server
        return _retryPolicy.ExecuteAsync(async () =>
        {
            await CreateServer();
            await _client.PostAsync($"servers/{_serverId}/start", null);
        });
    }

    public Task DisposeAsync()
    {
        // Stop and delete the server
        return _retryPolicy.ExecuteAsync(async () =>
        {
            await _client.PostAsync($"servers/{_serverId}/stop", null);
            await _client.DeleteAsync($"servers/{_serverId}");
        });
    }

    private async Task CreateServer()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "");
        request.Headers.Add("Content-Type", "multipart/form-data; boundary=---011000010111000001101001");

        MultipartFormDataContent formData = new("---011000010111000001101001")
        {
            { new StringContent(""), "name" },
            { new StringContent("cs2"), "game" },
            { new StringContent("cs2_settings.slots"), "5" },
            { new StringContent("cs2_settings.location"), "dusseldorf" },
            { new StringContent("cs2_settings.rcon_password"), _rconPassword }
        };

        request.Content = formData;
        HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        _serverId = await response.Content.ReadAsStringAsync();

        Console.WriteLine("{0}", await response.Content.ReadAsStringAsync());
    }
}

public class MyTests : IClassFixture<CsServerFixture>
{
    public MyTests(CsServerFixture fixture)
    {
        // You can access the fixture here if needed
    }

    // Your tests go here
}