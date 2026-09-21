using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ether.Api.Tests;

public sealed class AccountEndpointsTests
{
    [Fact]
    public async Task Create_account_returns_201_with_the_account()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var email = $"player-{Guid.NewGuid():N}@ether.local";

        var response = await client.PostAsJsonAsync(
            new Uri("/auth/register", UriKind.Relative), new { email, password = "strong-password" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty, document.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(email, document.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Create_character_returns_201_with_the_character()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);

        var response = await auth.Client.PostAsJsonAsync(
            new Uri($"/accounts/{auth.AccountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("characterId").GetGuid());
        Assert.Equal(auth.AccountId, root.GetProperty("accountId").GetGuid());
        Assert.Equal("Aragorn", root.GetProperty("name").GetString());
        Assert.Equal("Offline", root.GetProperty("state").GetString());
        Assert.Equal(1, root.GetProperty("level").GetInt32());
    }

    [Fact]
    public async Task Create_character_without_token_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            new Uri($"/accounts/{auth.AccountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_character_for_another_account_returns_403()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);

        var response = await auth.Client.PostAsJsonAsync(
            new Uri($"/accounts/{Guid.NewGuid()}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_character_with_invalid_class_returns_400()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);

        var response = await auth.Client.PostAsJsonAsync(
            new Uri($"/accounts/{auth.AccountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Wizard" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_character_with_duplicate_name_returns_409()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId, "Aragorn");

        var second = await auth.Client.PostAsJsonAsync(
            new Uri($"/accounts/{auth.AccountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Ranger" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_account_characters_returns_the_created_characters()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        await TestAuth.CreateCharacterAsync(auth.Client, auth.AccountId);

        var response = await auth.Client.GetAsync(
            new Uri($"/accounts/{auth.AccountId}/characters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Get_account_characters_for_another_account_returns_403()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);

        var response = await auth.Client.GetAsync(
            new Uri($"/accounts/{Guid.NewGuid()}/characters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_account_characters_without_token_returns_401()
    {
        using var factory = EtherApiFactory.Create();
        var auth = await TestAuth.RegisterAsync(factory);
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(
            new Uri($"/accounts/{auth.AccountId}/characters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
