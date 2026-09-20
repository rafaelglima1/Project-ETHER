namespace Ether.Application.Readiness;

/// <summary>
/// Overall readiness of the application, derived from its configured dependencies.
/// </summary>
public enum ReadinessStatus
{
    /// <summary>All configured dependencies are reachable (or none are configured).</summary>
    Ready = 0,

    /// <summary>At least one configured dependency is unreachable.</summary>
    NotReady = 1,
}

/// <summary>
/// Readiness of a single dependency.
/// </summary>
/// <param name="Name">Dependency name (e.g. <c>PostgreSQL</c>, <c>Redis</c>).</param>
/// <param name="Configured">Whether the dependency has a usable configuration.</param>
/// <param name="Healthy">Whether the dependency responded successfully.</param>
/// <param name="Detail">Human-readable detail, safe to expose (never secrets).</param>
public readonly record struct DependencyStatus(
    string Name,
    bool Configured,
    bool Healthy,
    string Detail)
{
    public static DependencyStatus NotConfigured(string name) =>
        new(name, Configured: false, Healthy: true, Detail: "not configured");

    public static DependencyStatus Ok(string name) =>
        new(name, Configured: true, Healthy: true, Detail: "ok");

    public static DependencyStatus Failed(string name, string detail) =>
        new(name, Configured: true, Healthy: false, Detail: detail);
}

/// <summary>
/// Result of a readiness probe.
/// </summary>
public sealed record ReadinessReport(ReadinessStatus Status, IReadOnlyList<DependencyStatus> Dependencies)
{
    public bool IsReady => Status == ReadinessStatus.Ready;

    public static ReadinessReport FromDependencies(IReadOnlyList<DependencyStatus> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        var ready = dependencies.All(d => d.Healthy);
        return new ReadinessReport(ready ? ReadinessStatus.Ready : ReadinessStatus.NotReady, dependencies);
    }
}
