using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Ether.Api.Tests;

/// <summary>An HTTP client authenticated as a freshly registered account.</summary>
internal sealed record AuthenticatedClient(
    HttpClient Client,
    Guid AccountId,
    string Email,
    string Password,
    string AccessToken,
    string RefreshToken);

internal static class TestAuth
{
    public static async Task<AuthenticatedClient> RegisterAsync(
        WebApplicationFactory<Program> factory,
        string? email = null,
        string password = "strong-password")
    {
        var client = factory.CreateClient();
        email ??= $"player-{Guid.NewGuid():N}@ether.local";

        var register = await client.PostAsJsonAsync(
            new Uri("/auth/register", UriKind.Relative), new { email, password });
        register.EnsureSuccessStatusCode();

        using var registered = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var accountId = registered.RootElement.GetProperty("accountId").GetGuid();

        var login = await client.PostAsJsonAsync(
            new Uri("/auth/login", UriKind.Relative), new { email, password });
        login.EnsureSuccessStatusCode();

        using var loggedIn = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = loggedIn.RootElement.GetProperty("accessToken").GetString()!;
        var refreshToken = loggedIn.RootElement.GetProperty("refreshToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return new AuthenticatedClient(client, accountId, email, password, accessToken, refreshToken);
    }

    public static async Task<Guid> CreateCharacterAsync(HttpClient client, Guid accountId, string? name = null)
    {
        name ??= $"Hero{Guid.NewGuid():N}"[..16];

        var response = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name, characterClass = "Warrior" });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("characterId").GetGuid();
    }
}
