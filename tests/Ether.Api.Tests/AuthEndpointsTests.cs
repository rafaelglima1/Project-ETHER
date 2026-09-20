using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ether.Api.Tests;

public sealed class AuthEndpointsTests
{
    private static string NewEmail() => $"player-{Guid.NewGuid():N}@ether.local";

    private static async Task<JsonDocument> RegisterAsync(HttpClient client, string email, string password = "strong-password")
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/auth/register", UriKind.Relative), new { email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_with_409()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var email = NewEmail();

        await RegisterAsync(client, email);

        var duplicate = await client.PostAsJsonAsync(
            new Uri("/auth/register", UriKind.Relative), new { email, password = "strong-password" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_invalid_email_with_400()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/auth/register", UriKind.Relative),
            new { email = "not-an-email", password = "strong-password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_tokens_and_me_accepts_the_access_token()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync(
            new Uri("/auth/login", UriKind.Relative), new { email, password = "strong-password" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = loginDocument.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/auth/me", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var me = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync(
            new Uri("/auth/login", UriKind.Relative), new { email, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/auth/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_issues_a_new_access_token()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        var login = await client.PostAsJsonAsync(
            new Uri("/auth/login", UriKind.Relative), new { email, password = "strong-password" });
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var refreshToken = loginDocument.RootElement.GetProperty("refreshToken").GetString();

        var refresh = await client.PostAsJsonAsync(
            new Uri("/auth/refresh", UriKind.Relative), new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        using var refreshDocument = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(refreshDocument.RootElement.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Refresh_with_invalid_token_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var refresh = await client.PostAsJsonAsync(
            new Uri("/auth/refresh", UriKind.Relative), new { refreshToken = "garbage" });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
