using System.Xml.Linq;

using AwesomeAssertions;

namespace Voyager.Architecture.Tests;

/// <summary>
/// Enforces the project dependency rules of docs/conventions/organisation-du-code.md (ADR-0002, ADR-0005).
/// These rules hold even before any type exists, because they read the project files themselves.
/// </summary>
[TestClass]
public sealed class ProjectReferenceTests
{
    private const string PLUGIN_PREFIX = "Voyager.Plugins.";
    private const string DEVELOPMENT_ORCHESTRATOR = "Voyager.AppHost";

    // Hosts assemble everything, so they are the only projects without restrictions.
    private static readonly HashSet<string> _hosts = ["Voyager.LocalService", "Voyager.GroupServer"];

    private static readonly Dictionary<string, string[]> _allowedReferences = new()
    {
        ["Voyager.Core"] = [],
        ["Voyager.Sdk"] = ["Voyager.Core"],
        ["Voyager.Contracts"] = [],
        ["Voyager.Persistence"] = ["Voyager.Core"],
        ["Voyager.Persistence.Sqlite"] = ["Voyager.Persistence"],
        ["Voyager.Persistence.PostgreSql"] = ["Voyager.Persistence"],
        ["Voyager.Sync"] = ["Voyager.Core", "Voyager.Contracts"],
        ["Voyager.PluginHost"] = ["Voyager.Sdk"],
        ["Voyager.ServiceDefaults"] = [],
        // The Aspire AppHost only orchestrates the hosts, in development (ADR-0011).
        [DEVELOPMENT_ORCHESTRATOR] = ["Voyager.LocalService", "Voyager.GroupServer"],
    };

    [TestMethod]
    public void ProjectReferences_ProductionProjects_PointOnlyToAllowedProjects()
    {
        var violations = RepositoryLayout.FindProductionProjects()
            .SelectMany(FindViolations)
            .ToList();

        violations.Should().BeEmpty("project dependencies must follow docs/conventions/organisation-du-code.md");
    }

    [TestMethod]
    public void ProjectReferences_ProductionProjects_AreAllFound()
    {
        var projectNames = RepositoryLayout.FindProductionProjects()
            .Select(project => Path.GetFileNameWithoutExtension(project.Name));

        projectNames.Should().Contain(_allowedReferences.Keys.Concat(_hosts));
    }

    // Development-only code must never ship: no project, not even a host, may reference the AppHost.
    [TestMethod]
    public void ProjectReferences_AnyProject_NeverPointToTheDevelopmentOrchestrator()
    {
        var offenders = RepositoryLayout.FindProductionProjects()
            .Where(project => ReadProjectReferences(project).Contains(DEVELOPMENT_ORCHESTRATOR))
            .Select(project => Path.GetFileNameWithoutExtension(project.Name));

        offenders.Should().BeEmpty($"{DEVELOPMENT_ORCHESTRATOR} is for development only (ADR-0011)");
    }

    private static IEnumerable<string> FindViolations(FileInfo project)
    {
        var name = Path.GetFileNameWithoutExtension(project.Name);
        if (_hosts.Contains(name))
        {
            return [];
        }

        string[]? allowed = name.StartsWith(PLUGIN_PREFIX, StringComparison.Ordinal)
            ? ["Voyager.Sdk"]
            : _allowedReferences.GetValueOrDefault(name);

        if (allowed is null)
        {
            return [$"{name} is not declared: add its allowed references here and in the organisation document"];
        }

        return ReadProjectReferences(project)
            .Where(reference => !allowed.Contains(reference))
            .Select(reference => $"{name} must not reference {reference}");
    }

    // Reads the project file itself: references added through Directory.Build.* imports are not seen,
    // so none may be added there.
    private static IEnumerable<string> ReadProjectReferences(FileInfo project) =>
        XDocument.Load(project.FullName)
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .SelectMany(include => include.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')));
}
