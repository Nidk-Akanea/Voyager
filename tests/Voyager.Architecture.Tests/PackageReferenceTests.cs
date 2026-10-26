using System.Xml.Linq;

using AwesomeAssertions;

namespace Voyager.Architecture.Tests;

/// <summary>
/// Enforces the package whitelist of ADR-0005 and ADR-0006: Voyager.Core and Voyager.Sdk only take the
/// libraries an ADR allows. Build-only tools shared by every project (Directory.Build.props) are not
/// runtime dependencies and are not listed here.
/// </summary>
[TestClass]
public sealed class PackageReferenceTests
{
    private static readonly Dictionary<string, string[]> _allowedPackages = new()
    {
        ["Voyager.Core"] = ["NodaTime"],
        ["Voyager.Sdk"] = [],
        // Shipped with both hosts: technical packages only, never a development-only Aspire.Hosting one.
        ["Voyager.ServiceDefaults"] =
        [
            "Microsoft.Extensions.Http.Resilience",
            "Microsoft.Extensions.ServiceDiscovery",
            "OpenTelemetry.Exporter.OpenTelemetryProtocol",
            "OpenTelemetry.Extensions.Hosting",
            "OpenTelemetry.Instrumentation.AspNetCore",
            "OpenTelemetry.Instrumentation.Http",
            "OpenTelemetry.Instrumentation.Runtime",
            "Serilog",
            "Serilog.Extensions.Logging",
            "Serilog.Sinks.File",
            "Serilog.Sinks.Seq",
        ],
    };

    [TestMethod]
    [DataRow("Voyager.Core")]
    [DataRow("Voyager.Sdk")]
    [DataRow("Voyager.ServiceDefaults")]
    public void PackageReferences_WhitelistedProject_UseOnlyAllowedPackages(string projectName)
    {
        var project = RepositoryLayout.FindProductionProjects()
            .Single(file => Path.GetFileNameWithoutExtension(file.Name) == projectName);

        var packages = XDocument.Load(project.FullName)
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>();

        packages.Should().BeSubsetOf(
            _allowedPackages[projectName],
            $"{projectName} only takes the libraries allowed by ADR-0006; a new one needs an ADR");
    }
}
