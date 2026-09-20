using Ether.Application.Readiness;

namespace Ether.Application.Abstractions;

/// <summary>
/// Probes the readiness of the application's configured dependencies.
/// Implemented by the infrastructure layer.
/// </summary>
public interface IDependencyReadinessProbe
{
    /// <summary>
    /// Checks only the dependencies that are actually configured.
    /// Must never throw for an unreachable dependency: failures are reported
    /// through <see cref="ReadinessReport"/> instead.
    /// </summary>
    Task<ReadinessReport> CheckAsync(CancellationToken cancellationToken = default);
}
