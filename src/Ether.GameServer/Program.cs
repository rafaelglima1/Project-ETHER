using Ether.Application.DependencyInjection;
using Ether.Contracts.Configuration;
using Ether.GameServer.DependencyInjection;
using Ether.GameServer.Realtime;
using Ether.Infrastructure.DependencyInjection;

// Aliased to avoid collision with Microsoft.AspNetCore.Builder.WebSocketOptions.
using WebSocketOptions = Ether.Contracts.Configuration.WebSocketOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEtherApplication()
    .AddEtherInfrastructure(builder.Configuration, builder.Environment.IsProduction())
    .AddEtherGameServer();

var webSocketOptions = builder.Configuration
                           .GetSection(WebSocketOptions.SectionName)
                           .Get<WebSocketOptions>()
                       ?? new WebSocketOptions();

var app = builder.Build();

app.UseWebSockets();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ether-game-server" }));

// Canonical realtime endpoint (ADR-0003). Transport only; rules live in Application/Domain.
app.MapGameWebSocket(webSocketOptions.Path);

app.Run();

/// <summary>Exposed for integration testing.</summary>
public partial class Program;
