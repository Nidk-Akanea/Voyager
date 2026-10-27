using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginManifestReaderTests
{
    [TestMethod]
    public void TryRead_CompleteManifest_ReturnsItsContent()
    {
        const string json = """
            {
              "id": "diagnostics-clock",
              "version": "0.1.0-alpha.3",
              "sdk": "0.1",
              "dotnet": { "assembly": "Voyager.Plugins.DiagnosticsClock.dll" },
              "ui": { "module": "index.js" },
              "provides": ["diagnostic-fact-source"]
            }
            """;

        var read = PluginManifestReader.TryRead(json, out var manifest);

        read.Should().BeTrue();
        manifest.Should().BeEquivalentTo(new PluginManifest(
            "diagnostics-clock",
            "0.1.0-alpha.3",
            "0.1",
            "Voyager.Plugins.DiagnosticsClock.dll",
            "index.js",
            ["diagnostic-fact-source"]));
    }

    [TestMethod]
    public void TryRead_PluginWithOnlyAnInterfacePart_IsValid()
    {
        const string json = """{ "id": "view-only", "version": "1.2.3", "sdk": "0.1", "ui": { "module": "views/main.mjs" } }""";

        PluginManifestReader.TryRead(json, out var manifest).Should().BeTrue();

        manifest!.DotnetAssembly.Should().BeNull();
        manifest.UiModule.Should().Be("views/main.mjs");
        manifest.Provides.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{ "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" } }""")]
    [DataRow("""{ "id": "Upper-Case", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" } }""")]
    [DataRow("""{ "id": "two--hyphens", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" } }""")]
    [DataRow("""{ "id": "a", "version": "one", "sdk": "0.1", "dotnet": { "assembly": "A.dll" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1.2", "dotnet": { "assembly": "A.dll" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1" }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": {} }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "sub/A.dll" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "..\\A.dll" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "ui": { "module": "../outside.js" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "ui": { "module": "/absolute.js" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "ui": { "module": "style.css" } }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "ui": "index.js" }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" }, "provides": ["unknown-point"] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" }, "provides": ["diagnostic-fact-source", "diagnostic-fact-source"] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" }, "provides": "diagnostic-fact-source" }""")]
    // A newline at the end must not slip through the end-of-line anchor of a pattern.
    [DataRow("{ \"id\": \"a\\n\", \"version\": \"1.0.0\", \"sdk\": \"0.1\", \"dotnet\": { \"assembly\": \"A.dll\" } }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\\n\", \"sdk\": \"0.1\", \"dotnet\": { \"assembly\": \"A.dll\" } }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\", \"sdk\": \"0.1\\n\", \"dotnet\": { \"assembly\": \"A.dll\" } }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\", \"sdk\": \"0.1\", \"dotnet\": { \"assembly\": \"A.dll\\n\" } }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\", \"sdk\": \"0.1\", \"ui\": { \"module\": \"index.js\\n\" } }")]
    public void TryRead_UnreadableOrIncompleteManifest_IsRefused(string json)
    {
        PluginManifestReader.TryRead(json, out var manifest).Should().BeFalse();

        manifest.Should().BeNull();
    }
}
