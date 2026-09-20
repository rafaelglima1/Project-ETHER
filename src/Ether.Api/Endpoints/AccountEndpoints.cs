using Ether.Application.Accounts;
using Ether.Application.Characters;
using Ether.Contracts.Characters;
using Ether.Domain.Accounts;

namespace Ether.Api.Endpoints;

/// <summary>Account and nested character endpoints (M1).</summary>
internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/accounts", async (
                CreateAccountHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                return Results.Created($"/accounts/{response.AccountId}/characters", response);
            })
            .WithName("CreateAccount")
            .WithTags("Accounts");

        app.MapPost("/accounts/{accountId:guid}/characters", async (
                Guid accountId,
                CreateCharacterRequest request,
                CreateCharacterHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(new AccountId(accountId), request, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Created($"/characters/{response.CharacterId}", response);
            })
            .WithName("CreateCharacter")
            .WithTags("Accounts", "Characters");

        app.MapGet("/accounts/{accountId:guid}/characters", async (
                Guid accountId,
                GetAccountCharactersHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(new AccountId(accountId), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(response);
            })
            .WithName("GetAccountCharacters")
            .WithTags("Accounts", "Characters");

        return app;
    }
}
