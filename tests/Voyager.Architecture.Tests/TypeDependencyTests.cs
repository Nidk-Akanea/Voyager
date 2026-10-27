using System.Reflection;

using AwesomeAssertions;

using NetArchTest.Rules;

namespace Voyager.Architecture.Tests;

/// <summary>
/// Enforces type-level dependency rules (ADR-0005, ADR-0006): what the code actually uses,
/// beyond the project references.
/// </summary>
[TestClass]
public sealed class TypeDependencyTests
{
    private static readonly string[] _technicalLibraries =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions",
        "Grpc",
        "Google.Protobuf",
        "Serilog",
        "OpenTelemetry",
        "Polly",
        "Aspire",
    ];

    private static readonly string[] _adapters =
    [
        "Voyager.Contracts",
        "Voyager.Persistence",
        "Voyager.Sync",
        "Voyager.PluginHost",
        "Voyager.LocalService",
        "Voyager.GroupServer",
        "Voyager.ServiceDefaults",
        "Voyager.AppHost",
    ];

    [TestMethod]
    public void Dependencies_CoreAssembly_IncludeNoTechnicalLibraryNorAdapter()
    {
        AssertNoDependency("Voyager.Core", [.. _technicalLibraries, .. _adapters, "Voyager.Sdk"]);
    }

    // The SDK shows plugins the two Microsoft abstractions of ADR-0015 and nothing else of Microsoft.Extensions.
    [TestMethod]
    public void Dependencies_SdkAssembly_IncludeNoTechnicalLibraryNorAdapter()
    {
        string[] forbiddenExtensions =
        [
            "Microsoft.Extensions.Caching",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.Diagnostics",
            "Microsoft.Extensions.FileProviders",
            "Microsoft.Extensions.Hosting",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Options",
        ];

        AssertNoDependency(
            "Voyager.Sdk",
            [.. _technicalLibraries.Where(library => library != "Microsoft.Extensions"), .. forbiddenExtensions, .. _adapters]);
    }

    [TestMethod]
    public void References_SdkAssembly_TakeOnlyTheTwoAllowedMicrosoftExtensionsAssemblies()
    {
        string[] allowed =
        [
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
        ];

        var references = Assembly.Load("Voyager.Sdk").GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));

        references.Should().BeSubsetOf(allowed, "the SDK only shows plugins dependency injection and logging abstractions (ADR-0015)");
    }

    [TestMethod]
    [DataRow("Shared", new[] { "Trips", "Catalog", "Quotes", "Bookings", "Budget" })]
    [DataRow("Catalog", new[] { "Trips", "Quotes", "Bookings", "Budget" })]
    [DataRow("Trips", new[] { "Catalog", "Quotes", "Bookings", "Budget" })]
    [DataRow("Quotes", new[] { "Bookings", "Budget" })]
    [DataRow("Bookings", new[] { "Budget" })]
    public void Dependencies_CoreDomain_PointOnlyToEarlierDomains(string domain, string[] laterDomains)
    {
        var result = Types.InAssembly(Assembly.Load("Voyager.Core"))
            .That().ResideInNamespace($"Voyager.Core.{domain}")
            .ShouldNot().HaveDependencyOnAny([.. laterDomains.Select(later => $"Voyager.Core.{later}")])
            .GetResult();

        (result.FailingTypeNames ?? []).Should().BeEmpty(
            $"Voyager.Core.{domain} must not depend on {string.Join(", ", laterDomains)}");
    }

    // Without this, a type outside the known domains (a typo, a new domain) would escape every rule above.
    [TestMethod]
    public void Namespaces_CoreTypes_ResideInSharedOrAKnownDomain()
    {
        string[] knownNamespaces = ["Shared", "Trips", "Catalog", "Quotes", "Bookings", "Budget"];

        var strayTypes = Assembly.Load("Voyager.Core").GetTypes()
            .Where(type => !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith("Voyager.Core", StringComparison.Ordinal))
            .Where(type => !knownNamespaces.Any(known =>
                type.Namespace == $"Voyager.Core.{known}" || type.Namespace!.StartsWith($"Voyager.Core.{known}.", StringComparison.Ordinal)))
            .Select(type => type.FullName);

        strayTypes.Should().BeEmpty("every type of Voyager.Core lives in Shared or in a domain declared in docs/conventions/organisation-du-code.md");
    }

    private static void AssertNoDependency(string assemblyName, string[] forbidden)
    {
        var result = Types.InAssembly(Assembly.Load(assemblyName))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        (result.FailingTypeNames ?? []).Should().BeEmpty($"{assemblyName} must not depend on {string.Join(", ", forbidden)}");
    }
}
