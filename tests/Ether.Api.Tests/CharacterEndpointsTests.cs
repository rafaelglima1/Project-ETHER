using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ether.Api.Tests;

public sealed class CharacterEndpointsTests
{
    [Fact]
    public async Task Get_character_returns_the_character()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);

        var response = await auth.Client.GetAsync(new Uri($"/characters/{characterId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(characterId, document.RootElement.GetProperty("characterId").GetGuid());
        Assert.Equal("Offline", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Get_unknown_character_returns_404()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);

        var response = await auth.Client.GetAsync(new Uri($"/characters/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_character_without_token_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(new Uri($"/characters/{characterId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_another_accounts_character_returns_403()
    {
        using var factory = EtherApiFactory.Create();
        var owner = await TestAuth.RegisterAsync(factory);
        var intruder = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(owner.Client, owner.AccountId);

        var response = await intruder.Client.GetAsync(new Uri($"/characters/{characterId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enter_world_puts_the_character_in_world()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);

        var response = await auth.Client.PostAsync(
            new Uri($"/characters/{characterId}/enter", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("InWorld", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Move_updates_the_authoritative_position()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);
        await auth.Client.PostAsync(new Uri($"/characters/{characterId}/enter", UriKind.Relative), content: null);

        var response = await auth.Client.PostAsJsonAsync(
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
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);
        await auth.Client.PostAsync(new Uri($"/characters/{characterId}/enter", UriKind.Relative), content: null);

        var response = await auth.Client.PostAsJsonAsync(
            new Uri($"/characters/{characterId}/move", UriKind.Relative), new { x = 31, y = 31 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Move_without_token_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            new Uri($"/characters/{characterId}/move", UriKind.Relative), new { x = 1, y = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Move_offline_character_returns_400()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);

        var response = await auth.Client.PostAsJsonAsync(
            new Uri($"/characters/{characterId}/move", UriKind.Relative), new { x = 1, y = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Game_token_is_issued_for_an_owned_character()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);

        var response = await auth.Client.PostAsync(
            new Uri($"/characters/{characterId}/game-token", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("gameToken").GetString()));
    }

    [Fact]
    public async Task Game_token_is_forbidden_for_another_accounts_character()
    {
        using var factory = EtherApiFactory.Create();
        var owner = await TestAuth.RegisterAsync(factory);
        var intruder = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(owner.Client, owner.AccountId);

        var response = await intruder.Client.PostAsync(
            new Uri($"/characters/{characterId}/game-token", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Game_token_is_not_accepted_as_an_access_token()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        var characterId = await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);

        var issued = await auth.Client.PostAsync(
            new Uri($"/characters/{characterId}/game-token", UriKind.Relative), content: null);
        using var document = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var gameToken = document.RootElement.GetProperty("gameToken").GetString()!;

        using var bearerAsGame = factory.CreateClient();
        bearerAsGame.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gameToken);

        var response = await bearerAsGame.GetAsync(new Uri("/auth/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_is_not_accepted_as_an_access_token()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);

        using var bearerAsRefresh = factory.CreateClient();
        bearerAsRefresh.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.RefreshToken);

        var response = await bearerAsRefresh.GetAsync(new Uri("/auth/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
