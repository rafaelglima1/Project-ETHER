using Ether.Application.Characters;
using Ether.Domain.Characters;

namespace Ether.Api.Endpoints;

/// <summary>Character endpoints (M1).</summary>
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

        return app;
    }
}
