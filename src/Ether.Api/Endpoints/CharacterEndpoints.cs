using System.Security.Claims;

using Ether.Api.Auth;
using Ether.Application.Characters;
using Ether.Contracts.Characters;
using Ether.Domain.Characters;

namespace Ether.Api.Endpoints;

/// <summary>Character endpoints (M1/M3), all account-scoped and authenticated.</summary>
internal static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/characters/{characterId:guid}", async (
                Guid characterId,
                ClaimsPrincipal user,
                GetCharacterHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new CharacterId(characterId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("GetCharacter")
            .WithTags("Characters");

        app.MapPost("/characters/{characterId:guid}/enter", async (
                Guid characterId,
                ClaimsPrincipal user,
                EnterWorldHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new CharacterId(characterId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("EnterWorld")
            .WithTags("Characters");

        app.MapPost("/characters/{characterId:guid}/move", async (
                Guid characterId,
                MoveCharacterRequest request,
                ClaimsPrincipal user,
                MoveCharacterHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new CharacterId(characterId), request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("MoveCharacter")
            .WithTags("Characters");

        app.MapPost("/characters/{characterId:guid}/game-token", async (
                Guid characterId,
                ClaimsPrincipal user,
                IssueGameTokenHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new CharacterId(characterId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("IssueGameToken")
            .WithTags("Characters", "Auth");

        return app;
    }
}
