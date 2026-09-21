using System.Reflection;

namespace Ether.Architecture.Tests;

/// <summary>
/// Enforces the dependency rules defined by Blueprint v5.0 §76 and the M0 plan.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = Assembly.Load("Ether.Domain");
    private static readonly Assembly Application = Assembly.Load("Ether.Application");
    private static readonly Assembly Contracts = Assembly.Load("Ether.Contracts");
    private static readonly Assembly Infrastructure = Assembly.Load("Ether.Infrastructure");
    private static readonly Assembly Api = Assembly.Load("Ether.Api");
    private static readonly Assembly GameServer = Assembly.Load("Ether.GameServer");
    private static readonly Assembly Worker = Assembly.Load("Ether.Worker");

    private static IEnumerable<Assembly> AllAssemblies
    {
        get
        {
            yield return Domain;
            yield return Application;
            yield return Contracts;
            yield return Infrastructure;
            yield return Api;
            yield return GameServer;
            yield return Worker;
        }
    }

    [Fact]
    public void All_assemblies_contain_types()
    {
        foreach (var assembly in AllAssemblies)
        {
            Assert.NotEmpty(assembly.GetTypes());
        }
    }

    [Fact]
    public void Domain_must_not_depend_on_other_layers()
    {
        AssertNoForbiddenDependencies(
            Domain,
            "Ether.Application",
            "Ether.Infrastructure",
            "Ether.Api",
            "Ether.GameServer",
            "Ether.Worker",
            "Ether.Contracts");
    }

    [Fact]
    public void Domain_must_not_depend_on_infrastructure_technologies()
    {
        AssertNoForbiddenDependencies(
            Domain,
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql",
            "StackExchange.Redis",
            "Godot");
    }

    [Fact]
    public void Application_must_not_depend_on_infrastructure_or_presentation()
    {
        AssertNoForbiddenDependencies(
            Application,
            "Ether.Infrastructure",
            "Ether.Api",
            "Ether.GameServer",
            "Ether.Worker",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql",
            "StackExchange.Redis",
            "Godot");
    }

    [Fact]
    public void Contracts_must_remain_dependency_light()
    {
        AssertNoForbiddenDependencies(
            Contracts,
            "Ether.Domain",
            "Ether.Application",
            "Ether.Infrastructure",
            "Ether.Api",
            "Ether.GameServer",
            "Ether.Worker",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql",
            "StackExchange.Redis",
            "Godot");
    }

    [Fact]
    public void GameServer_must_not_depend_on_client_or_godot()
    {
        AssertNoForbiddenDependencies(GameServer, "Godot", "Ether.Client");
    }

    [Fact]
    public void Worker_must_not_depend_on_presentation_or_godot()
    {
        // Worker may depend on Application + Infrastructure (and transitively
        // Contracts/Domain), but must not depend on presentation layers or the client.
        AssertNoForbiddenDependencies(
            Worker,
            "Ether.Api",
            "Ether.GameServer",
            "Godot",
            "Ether.Client");
    }

    [Fact]
    public void GameServer_must_not_depend_on_entity_framework()
    {
        // The realtime transport must not touch the database directly; persistence
        // goes through Application abstractions implemented by Infrastructure.
        AssertNoForbiddenDependencies(
            GameServer,
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Ether.Infrastructure.Persistence");
    }

    [Fact]
    public void Api_must_not_depend_on_client_or_godot()
    {
        AssertNoForbiddenDependencies(Api, "Godot", "Ether.Client");
    }

    [Fact]
    public void No_assembly_references_a_godot_assembly()
    {
        foreach (var assembly in AllAssemblies)
        {
            var references = AssemblyDependencies.CollectReferencedAssemblies(assembly);
            Assert.DoesNotContain(
                references,
                name => name.Contains("Godot", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Required_layer_dependencies_are_present()
    {
        Assert.True(
            AssemblyDependencies.DependsOnNamespace(Infrastructure, "Ether.Application"),
            "Infrastructure must depend on Application.");

        Assert.True(
            AssemblyDependencies.DependsOnNamespace(Api, "Ether.Infrastructure"),
            "Api must depend on Infrastructure.");

        Assert.True(
            AssemblyDependencies.DependsOnNamespace(GameServer, "Ether.Infrastructure"),
            "GameServer must depend on Infrastructure.");

        Assert.True(
            AssemblyDependencies.DependsOnNamespace(Worker, "Ether.Infrastructure"),
            "Worker must depend on Infrastructure.");

        Assert.True(
            AssemblyDependencies.DependsOnNamespace(Worker, "Ether.Application"),
            "Worker must depend on Application.");
    }

    private static void AssertNoForbiddenDependencies(Assembly assembly, params string[] forbiddenRoots)
    {
        var namespaces = AssemblyDependencies.CollectNamespaces(assembly);

        var violations = forbiddenRoots
            .Where(root => namespaces.Any(ns => AssemblyDependencies.Matches(ns, root)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{assembly.GetName().Name} has forbidden dependencies: {string.Join(", ", violations)}");
    }
}
