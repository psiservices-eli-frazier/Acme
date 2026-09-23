using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Acme.Server.Tests;

/// <summary>
/// An HTTP client that knows about the cookie and the antiforgery token.
///
/// The token is fetched lazily and dropped after sign-in, because it is bound to the
/// identity that requested it -- a token obtained while anonymous stops validating the
/// moment the caller is authenticated. The browser client does the same thing.
/// </summary>
public sealed class ApiSession
{
    private readonly HttpClient _client;
    private string? _token;

    private ApiSession(HttpClient client) => _client = client;

    public static ApiSession Anonymous(AcmeApiFactory factory) => new(factory.CreateClient());

    public static async Task<ApiSession> SignInAsync(AcmeApiFactory factory, string username, string password)
    {
        var session = new ApiSession(factory.CreateClient());

        var response = await session.SendAsync(HttpMethod.Post, "/api/auth/login", new { username, password });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Sign-in as '{username}' failed: {response.StatusCode}");
        }

        session._token = null;
        return session;
    }

    /// <summary>Sends without an antiforgery header, to prove the header is required.</summary>
    public Task<HttpResponseMessage> SendWithoutTokenAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    private async Task<string> TokenAsync()
    {
        if (_token is null)
        {
            var body = await _client.GetStringAsync("/api/antiforgery/token");
            _token = JsonNode.Parse(body)!["token"]!.GetValue<string>();
        }

        return _token;
    }

    public Task<HttpResponseMessage> GetAsync(string path) => _client.GetAsync(path);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", await TokenAsync());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string path, object body) =>
        SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object body) =>
        SendAsync(HttpMethod.Put, path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path);

    /// <summary>Reads the body as JSON, failing the test if the status is not 2xx.</summary>
    public static async Task<JsonNode> OkJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!;
    }

    /// <summary>Reads the body as JSON whatever the status.</summary>
    public static async Task<JsonNode> JsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    public async Task<JsonNode> GetJsonAsync(string path) => await OkJsonAsync(await GetAsync(path));
}
