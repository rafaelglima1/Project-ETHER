using Ether.Application.Exceptions;
using Ether.Domain.Common;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ether.Api.Errors;

/// <summary>
/// Translates known domain/application failures into deterministic HTTP responses.
/// Unknown failures fall through to the default handler (500).
/// </summary>
internal sealed class EtherExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            DomainException => StatusCodes.Status400BadRequest,
            AccountNotFoundException => StatusCodes.Status404NotFound,
            CharacterNotFoundException => StatusCodes.Status404NotFound,
            DuplicateCharacterNameException => StatusCodes.Status409Conflict,
            DuplicateEmailException => StatusCodes.Status409Conflict,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            InvalidTokenException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrase(statusCode),
            Detail = exception.Message,
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Error",
    };
}
