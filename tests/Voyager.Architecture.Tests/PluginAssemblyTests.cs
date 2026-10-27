using System.Reflection;

using AwesomeAssertions;

namespace Voyager.Architecture.Tests;

/// <summary>
/// Enforces what the assembly of a plugin may depend on (ADR-0015, decision 5). It replaces a check of
/// the project references alone, which transitivity makes insufficient: it reads what the compiled
/// assembly really references.
/// </summary>
[TestClass]
public sealed class PluginAssemblyTests
{
    // Shipped plugins, and the conforming test plugin that keeps this check from passing on nothing.
    private static readonly string[] _pluginAssemblyPrefixes = ["Voyager.Plugins.", "Voyager.PluginFixtures.Good"];

    // The assemblies the host shares with every plugin, plus the platform.
    private static readonly string[] _allowedAssemblies =
    [
        "Voyager.Sdk",
        "Voyager.Core",
        "NodaTime",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
    ];

    private static readonly string[] _platformPrefixes = ["System.", "netstandard", "mscorlib"];

    [TestMethod]
    public void References_PluginAssemblies_TakeOnlyTheSdkSharedTypesAndThePlatform()
    {
        var plugins = FindPluginAssemblies().ToList();
        var violations = plugins.SelectMany(FindViolations).ToList();

        plugins.Should().NotBeEmpty("a check that finds no plugin assembly proves nothing");

        violations.Should().BeEmpty("a plugin only depends on Voyager.Sdk, shared types, NodaTime, two Microsoft abstractions and the platform");
    }

    // A rule that cannot fail proves nothing: the SQLite adapter references the persistence layer, which a plugin must not.
    [TestMethod]
    public void FindViolations_AssemblyThatReferencesInfrastructure_ReportsIt()
    {
        var violations = FindViolations(typeof(Voyager.Persistence.Sqlite.SqlitePersistenceExtensions).Assembly).ToList();

        violations.Should().Contain(violation => violation.Contains("Voyager.Persistence", StringComparison.Ordinal));
    }

    private static IEnumerable<Assembly> FindPluginAssemblies() =>
        _pluginAssemblyPrefixes
            .SelectMany(prefix => Directory.EnumerateFiles(AppContext.BaseDirectory, prefix + "*.dll"))
            .Select(Assembly.LoadFrom);

    private static IEnumerable<string> FindViolations(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => !_allowedAssemblies.Contains(name)
                && !_platformPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(name => $"{assembly.GetName().Name} references {name}");
}
