using System.Security.Claims;

using Ether.Application.Accounts;
using Ether.Application.Auth;
using Ether.Contracts.Auth;
using Ether.Domain.Accounts;

namespace Ether.Api.Endpoints;

/// <summary>Authentication and session endpoints (M2).</summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/auth/register", async (
                RegisterAccountRequest request,
                RegisterAccountHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return Results.Created($"/accounts/{response.AccountId}", response);
            })
            .WithName("RegisterAccount")
            .WithTags("Auth");

        app.MapPost("/auth/login", async (
                LoginRequest request,
                LoginHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(response);
            })
            .WithName("Login")
            .WithTags("Auth");

        app.MapPost("/auth/refresh", async (
                RefreshRequest request,
                RefreshTokenHandler handler,
                CancellationToken cancellationToken) =>
            {
                var response = await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(response);
            })
            .WithName("RefreshToken")
            .WithTags("Auth");

        app.MapGet("/auth/me", (ClaimsPrincipal user) =>
            {
                var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value;

                return Guid.TryParse(subject, out var accountId)
                    ? Results.Ok(new { accountId })
                    : Results.Unauthorized();
            })
            .RequireAuthorization()
            .WithName("Me")
            .WithTags("Auth");

        return app;
    }
}
