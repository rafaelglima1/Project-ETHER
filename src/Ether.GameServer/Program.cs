using Ether.Application.DependencyInjection;
using Ether.GameServer.DependencyInjection;
using Ether.Infrastructure.DependencyInjection;

using Microsoft.Extensions.Options;

// Aliased to avoid collision with Microsoft.AspNetCore.Builder.WebSocketOptions.
using WebSocketOptions = Ether.Contracts.Configuration.WebSocketOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEtherApplication()
    .AddEtherInfrastructure(builder.Configuration)
    .AddEtherGameServer();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ether-game-server" }));

// Gameplay endpoint placeholder. The WebSocket pipeline is added in a later
// milestone; M0 only exposes the configured path so the surface is explicit.
app.MapGet("/game", (IOptions<WebSocketOptions> options) => Results.Ok(new
{
    service = "ether-game-server",
    protocol = "websocket-not-implemented",
    path = options.Value.Path,
}));

app.Run();

/// <summary>Exposed for integration testing.</summary>
public partial class Program;
