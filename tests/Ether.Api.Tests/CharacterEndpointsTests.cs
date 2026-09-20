using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ether.Api.Tests;

public sealed class CharacterEndpointsTests
{
    private static async Task<(Guid AccountId, Guid CharacterId)> SeedCharacterAsync(HttpClient client)
    {
        var accountResponse = await client.PostAsJsonAsync(
            new Uri("/auth/register", UriKind.Relative),
            new { email = $"player-{Guid.NewGuid():N}@ether.local", password = "strong-password" });
        using var accountDocument = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
        var accountId = accountDocument.RootElement.GetProperty("accountId").GetGuid();

        var createdResponse = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = $"Hero{Guid.NewGuid():N}"[..16], characterClass = "Warrior" });
        using var createdDocument = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync());
        var characterId = createdDocument.RootElement.GetProperty("characterId").GetGuid();

        return (accountId, characterId);
    }

    [Fact]
    public async Task Get_character_returns_the_character()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var (_, characterId) = await SeedCharacterAsync(client);

        var response = await client.GetAsync(new Uri($"/characters/{characterId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(characterId, document.RootElement.GetProperty("characterId").GetGuid());
        Assert.Equal("Offline", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Get_unknown_character_returns_404()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/characters/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enter_world_puts_the_character_in_world()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var (_, characterId) = await SeedCharacterAsync(client);

        var response = await client.PostAsync(
            new Uri($"/characters/{characterId}/enter", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("InWorld", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Move_updates_the_authoritative_position()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var (_, characterId) = await SeedCharacterAsync(client);
        await client.PostAsync(new Uri($"/characters/{characterId}/enter", UriKind.Relative), content: null);

        var response = await client.PostAsJsonAsync(
            new Uri($"/characters/{characterId}/move", UriKind.Relative), new { x = 3, y = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, document.RootElement.GetProperty("positionX").GetInt32());
        Assert.Equal(5, document.RootElement.GetProperty("positionY").GetInt32());
    }

    [Fact]
    public async Task Move_beyond_allowed_distance_returns_400()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var (_, characterId) = await SeedCharacterAsync(client);
        await client.PostAsync(new Uri($"/characters/{characterId}/enter", UriKind.Relative), content: null);

        var response = await client.PostAsJsonAsync(
            new Uri($"/characters/{characterId}/move", UriKind.Relative), new { x = 31, y = 31 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Move_offline_character_returns_400()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var (_, characterId) = await SeedCharacterAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/characters/{characterId}/move", UriKind.Relative), new { x = 1, y = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
