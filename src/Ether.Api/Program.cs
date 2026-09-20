using System.Text;

using Ether.Api.Endpoints;
using Ether.Api.Errors;
using Ether.Application.Abstractions;
using Ether.Application.DependencyInjection;
using Ether.Contracts.Configuration;
using Ether.Infrastructure.DependencyInjection;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEtherApplication()
    .AddEtherInfrastructure(builder.Configuration, builder.Environment.IsProduction());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<EtherExceptionHandler>();

var authenticationOptions = builder.Configuration
                                .GetSection(AuthenticationOptions.SectionName)
                                .Get<AuthenticationOptions>()
                            ?? new AuthenticationOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authenticationOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authenticationOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(authenticationOptions.ResolveSigningKey())),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// Liveness: the process is up. Always 200 while the app is running.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ether-api" }));

// Readiness: reflects the state of configured dependencies (PostgreSQL, Redis).
app.MapGet("/ready", async (IDependencyReadinessProbe probe, CancellationToken cancellationToken) =>
{
    var report = await probe.CheckAsync(cancellationToken).ConfigureAwait(false);

    var payload = new
    {
        status = report.IsReady ? "ready" : "not_ready",
        dependencies = report.Dependencies.Select(dependency => new
        {
            name = dependency.Name,
            configured = dependency.Configured,
            healthy = dependency.Healthy,
            detail = dependency.Detail,
        }),
    };

    return report.IsReady
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapCharacterEndpoints();

app.Run();

/// <summary>Exposed for integration testing.</summary>
public partial class Program;
