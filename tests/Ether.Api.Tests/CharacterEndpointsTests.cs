using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ether.Api.Tests;

public sealed class CharacterEndpointsTests
{
    [Fact]
    public async Task Get_character_returns_the_character()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var accountResponse = await client.PostAsJsonAsync(new Uri("/accounts", UriKind.Relative), new { });
        using var accountDocument = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
        var accountId = accountDocument.RootElement.GetProperty("accountId").GetGuid();

        var createdResponse = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "Legolas", characterClass = "Ranger" });
        using var createdDocument = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync());
        var characterId = createdDocument.RootElement.GetProperty("characterId").GetGuid();

        var response = await client.GetAsync(new Uri($"/characters/{characterId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(characterId, document.RootElement.GetProperty("characterId").GetGuid());
        Assert.Equal("Legolas", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Get_unknown_character_returns_404()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/characters/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
