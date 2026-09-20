using Ether.Application.Characters;
using Ether.Contracts.Characters;
using Ether.Domain.Characters;

namespace Ether.Api.Endpoints;

/// <summary>Character endpoints (M1/M3).</summary>
internal static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacterEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/characters/{characterId:guid}", async (
                Guid characterId,
                GetCharacterHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(new CharacterId(characterId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .WithName("GetCharacter")
            .WithTags("Characters");

        app.MapPost("/characters/{characterId:guid}/enter", async (
                Guid characterId,
                EnterWorldHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(new CharacterId(characterId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .WithName("EnterWorld")
            .WithTags("Characters");

        app.MapPost("/characters/{characterId:guid}/move", async (
                Guid characterId,
                MoveCharacterRequest request,
                MoveCharacterHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(new CharacterId(characterId), request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .WithName("MoveCharacter")
            .WithTags("Characters");

        return app;
    }
}
