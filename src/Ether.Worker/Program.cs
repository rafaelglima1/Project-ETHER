using Ether.Application.DependencyInjection;
using Ether.Infrastructure.DependencyInjection;
using Ether.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddEtherApplication()
    .AddEtherInfrastructure(builder.Configuration, builder.Environment.IsProduction());

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
