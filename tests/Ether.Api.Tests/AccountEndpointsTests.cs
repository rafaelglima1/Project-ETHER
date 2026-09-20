using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Ether.Api.Tests;

public sealed class AccountEndpointsTests
{
    private static async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(new Uri("/accounts", UriKind.Relative), new { });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accountId").GetGuid();
    }

    [Fact]
    public async Task Create_account_returns_201_with_the_account()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(new Uri("/accounts", UriKind.Relative), new { });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("accountId").GetGuid());
        Assert.Equal("Active", root.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_character_returns_201_with_the_character()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("characterId").GetGuid());
        Assert.Equal(accountId, root.GetProperty("accountId").GetGuid());
        Assert.Equal("Aragorn", root.GetProperty("name").GetString());
        Assert.Equal("Warrior", root.GetProperty("characterClass").GetString());
        Assert.Equal("Offline", root.GetProperty("state").GetString());
        Assert.Equal(1, root.GetProperty("level").GetInt32());
        Assert.Equal(0, root.GetProperty("experience").GetInt64());
    }

    [Fact]
    public async Task Create_character_for_unknown_account_returns_404()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri($"/accounts/{Guid.NewGuid()}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_character_with_invalid_class_returns_400()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Wizard" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_character_with_empty_name_returns_400()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "  ", characterClass = "Warrior" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_character_with_duplicate_name_returns_409()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var accountId = await CreateAccountAsync(client);

        var first = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Ranger" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_account_characters_returns_the_created_characters()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();
        var accountId = await CreateAccountAsync(client);

        await client.PostAsJsonAsync(
            new Uri($"/accounts/{accountId}/characters", UriKind.Relative),
            new { name = "Aragorn", characterClass = "Warrior" });

        var response = await client.GetAsync(new Uri($"/accounts/{accountId}/characters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Get_account_characters_for_unknown_account_returns_404()
    {
        using var factory = EtherApiFactory.Create();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            new Uri($"/accounts/{Guid.NewGuid()}/characters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
