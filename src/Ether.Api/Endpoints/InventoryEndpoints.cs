using System.Security.Claims;

using Ether.Api.Auth;
using Ether.Application.Inventory;
using Ether.Contracts.Characters;
using Ether.Domain.Characters;

namespace Ether.Api.Endpoints;

/// <summary>Inventory query endpoint (M8).</summary>
internal static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/characters/{characterId:guid}/inventory", async (
                Guid characterId,
                ClaimsPrincipal user,
                GetInventoryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new CharacterId(characterId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("GetInventory")
            .WithTags("Characters", "Inventory");

        return app;
    }
}
