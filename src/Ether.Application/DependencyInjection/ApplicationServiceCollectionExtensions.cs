using Microsoft.Extensions.DependencyInjection;

namespace Ether.Application.DependencyInjection;

/// <summary>
/// Registration entry point for the application layer.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services. Use cases are added in later milestones (M1+).
    /// </summary>
    public static IServiceCollection AddEtherApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
