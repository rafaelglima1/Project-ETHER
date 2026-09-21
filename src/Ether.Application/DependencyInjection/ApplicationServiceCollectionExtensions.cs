using Ether.Application.Accounts;
using Ether.Application.Auth;
using Ether.Application.Characters;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ether.Application.DependencyInjection;

/// <summary>
/// Registration entry point for the application layer.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services and use cases.</summary>
    public static IServiceCollection AddEtherApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<RegisterAccountHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<CreateCharacterHandler>();
        services.AddScoped<GetCharacterHandler>();
        services.AddScoped<GetAccountCharactersHandler>();
        services.AddScoped<EnterWorldHandler>();
        services.AddScoped<MoveCharacterHandler>();
        services.AddScoped<IssueGameTokenHandler>();

        return services;
    }
}
