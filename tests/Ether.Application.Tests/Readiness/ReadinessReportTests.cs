using Ether.Application.Readiness;

namespace Ether.Application.Tests.Readiness;

public sealed class ReadinessReportTests
{
    [Fact]
    public void No_dependencies_is_ready()
    {
        var report = ReadinessReport.FromDependencies([]);

        Assert.True(report.IsReady);
        Assert.Equal(ReadinessStatus.Ready, report.Status);
    }

    [Fact]
    public void All_dependencies_healthy_is_ready()
    {
        var report = ReadinessReport.FromDependencies(
        [
            DependencyStatus.NotConfigured("PostgreSQL"),
            DependencyStatus.Ok("Redis"),
        ]);

        Assert.True(report.IsReady);
    }

    [Fact]
    public void Any_failed_dependency_is_not_ready()
    {
        var report = ReadinessReport.FromDependencies(
        [
            DependencyStatus.Ok("PostgreSQL"),
            DependencyStatus.Failed("Redis", "connection refused"),
        ]);

        Assert.False(report.IsReady);
        Assert.Equal(ReadinessStatus.NotReady, report.Status);
    }

    [Fact]
    public void Null_dependencies_throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReadinessReport.FromDependencies(null!));
    }
}
