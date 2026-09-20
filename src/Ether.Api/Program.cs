using Ether.Application.Abstractions;
using Ether.Application.DependencyInjection;
using Ether.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEtherApplication()
    .AddEtherInfrastructure(builder.Configuration);

var app = builder.Build();

// Liveness: the process is up. Always 200 while the app is running.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ether-api" }));

// Readiness: reflects the state of configured dependencies (PostgreSQL, Redis).
// Unconfigured dependencies are reported as "not configured" and do not fail readiness.
// Configured but unreachable dependencies return 503 (environment blocker is visible).
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

app.Run();

/// <summary>Exposed for integration testing.</summary>
public partial class Program;
