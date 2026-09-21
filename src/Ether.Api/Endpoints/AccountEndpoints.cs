using System.Security.Claims;

using Ether.Api.Auth;
using Ether.Application.Characters;
using Ether.Contracts.Characters;
using Ether.Domain.Accounts;

namespace Ether.Api.Endpoints;

/// <summary>Character endpoints nested under an account (M1/M3).</summary>
internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/accounts/{accountId:guid}/characters", async (
                Guid accountId,
                CreateCharacterRequest request,
                ClaimsPrincipal user,
                CreateCharacterHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new AccountId(accountId), request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Created($"/characters/{response.CharacterId}", response);
            })
            .RequireAuthorization()
            .WithName("CreateCharacter")
            .WithTags("Accounts", "Characters");

        app.MapGet("/accounts/{accountId:guid}/characters", async (
                Guid accountId,
                ClaimsPrincipal user,
                GetAccountCharactersHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler
                    .HandleAsync(user.AccountId(), new AccountId(accountId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("GetAccountCharacters")
            .WithTags("Accounts", "Characters");

        return app;
    }
}
