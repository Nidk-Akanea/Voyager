using System.Text.RegularExpressions;

using AwesomeAssertions;

namespace Voyager.Architecture.Tests;

/// <summary>
/// Keeps the version configurations of the components consistent (ADR-0007): the application, the SDK and the
/// plugins have one number each, computed with the same rules from their own history.
/// </summary>
[TestClass]
public sealed partial class GitVersionConfigurationTests
{
    private const string APPLICATION_CONFIGURATION = "GitVersion.yml";
    private const string SDK_CONFIGURATION = "src/dotnet/Voyager.Sdk/GitVersion.yml";

    // What turns commits into a version: identical everywhere, so that a feature is a minor version for any component.
    private static readonly string[] _sharedKeys =
    [
        "workflow",
        "next-version",
        "major-version-bump-message",
        "minor-version-bump-message",
        "patch-version-bump-message",
        "no-bump-message",
        "commit-message-incrementing",
    ];

    // The folders the SDK is made of: what its configuration counts.
    private static readonly string[] _sdkFolders = ["src/dotnet/Voyager.Sdk", "src/ts/plugin-sdk"];

    [GeneratedRegex(@"^\s*<PackageVersion Include=""GitVersion\.MsBuild"" Version=""(?<version>[^""]+)""", RegexOptions.Multiline)]
    private static partial Regex GitVersionMsBuildVersion();

    [GeneratedRegex(@"GitVersion\.Tool@(?<version>[0-9][^'""\s]*)")]
    private static partial Regex GitVersionToolVersion();

    [TestMethod]
    public void Rules_SdkConfiguration_AreThoseOfTheApplication()
    {
        var application = ReadTopLevelValues(ReadConfiguration(APPLICATION_CONFIGURATION));
        var sdk = ReadTopLevelValues(ReadConfiguration(SDK_CONFIGURATION));

        foreach (var key in _sharedKeys)
        {
            application.Should().ContainKey(key);
            sdk.Should().ContainKey(key);
            sdk[key].Should().Be(application[key], $"the SDK follows the rule '{key}' of the application");
        }
    }

    // GitFlow gives a release branch the version of its name and every develop a new minor version: a component that
    // did not change would get the number of the application, and the host would refuse every plugin (ADR-0015).
    [TestMethod]
    public void Number_SdkConfiguration_ComesFromTheTagsAndNotFromTheBranch()
    {
        var configuration = string.Join('\n', ReadConfiguration(SDK_CONFIGURATION));

        configuration.Should().Contain("ConfiguredNextVersion");
        configuration.Should().Contain("TaggedCommit");
        configuration.Should().NotContain("VersionInBranchName");
        foreach (var branch in new[] { "main", "develop", "release", "hotfix" })
        {
            configuration.Should().MatchRegex($@"(?m)^  {branch}:\n    increment: None$", $"{branch} adds no version of its own");
        }
    }

    // tools/get-version.ps1 builds the configuration of a plugin from this file, replacing these two keys.
    [TestMethod]
    public void Template_SdkConfiguration_HasTheTagPrefixAndTheIgnoredPathsThatPluginsReplace()
    {
        var lines = ReadConfiguration(SDK_CONFIGURATION);

        lines.Should().Contain("tag-prefix: '(sdk/v)?'", "the prefix is optional as a whole: next-version is read with it");
        lines.Should().Contain("ignore:");
    }

    // A regular expression naming a folder that moved silently counts nothing: the SDK would never change version.
    [TestMethod]
    public void Paths_SdkConfiguration_NameFoldersThatExist()
    {
        var root = RepositoryLayout.FindRoot();
        var configuration = string.Join('\n', ReadConfiguration(SDK_CONFIGURATION));

        foreach (var folder in _sdkFolders)
        {
            Directory.Exists(Path.Combine(root.FullName, folder)).Should().BeTrue($"{folder} is part of the SDK");
            configuration.Should().Contain(folder.Replace(".", "\\.", StringComparison.Ordinal) + "/");
        }
    }

    // The build stamps the assemblies with GitVersion.MsBuild, the scripts and the CI compute with the command-line
    // tool: two versions of GitVersion could give two numbers to the same component.
    [TestMethod]
    public void Version_GitVersionTool_IsTheOneOfGitVersionMsBuild()
    {
        var root = RepositoryLayout.FindRoot().FullName;
        var packages = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        var script = File.ReadAllText(Path.Combine(root, "tools", "get-version.ps1"));

        var msBuild = GitVersionMsBuildVersion().Match(packages);
        var tool = GitVersionToolVersion().Match(script);

        msBuild.Success.Should().BeTrue("Directory.Packages.props pins GitVersion.MsBuild");
        tool.Success.Should().BeTrue("tools/get-version.ps1 pins GitVersion.Tool");
        tool.Groups["version"].Value.Should().Be(msBuild.Groups["version"].Value);
    }

    private static List<string> ReadConfiguration(string relativePath) =>
        [.. File.ReadAllLines(Path.Combine(RepositoryLayout.FindRoot().FullName, relativePath))
            .Select(line => line.TrimEnd())
            .Where(line => !line.StartsWith('#'))];

    private static Dictionary<string, string> ReadTopLevelValues(IEnumerable<string> lines) =>
        lines
            .Where(line => line.Length > 0 && !line.StartsWith(' ') && line.Contains(':', StringComparison.Ordinal))
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0], parts => parts[1].Trim(), StringComparer.Ordinal);
}
