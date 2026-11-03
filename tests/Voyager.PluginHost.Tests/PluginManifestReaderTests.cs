using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginManifestReaderTests
{
    #region Public Methods

    [TestMethod]
    [DataRow("con")]
    [DataRow("prn")]
    [DataRow("aux")]
    [DataRow("nul")]
    [DataRow("com0")]
    [DataRow("com1")]
    [DataRow("lpt0")]
    [DataRow("lpt9")]
    public void IsValidIdentifier_NameThatWindowsGivesToADevice_IsRefused(string identifier)
    {
        PluginManifestReader.IsValidIdentifier(identifier).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("console")]
    [DataRow("com10")]
    [DataRow("diagnostics-clock")]
    public void IsValidIdentifier_OrdinaryName_IsAccepted(string identifier)
    {
        PluginManifestReader.IsValidIdentifier(identifier).Should().BeTrue();
    }

    [TestMethod]
    public void TryRead_CompleteManifest_ReturnsItsContent()
    {
        const string json = """
            {
              "id": "diagnostics-clock",
              "version": "0.1.0-alpha.3",
              "dotnet": { "assembly": "Voyager.Plugins.DiagnosticsClock.dll" },
              "ui": { "module": "index.js" },
              "provides": [{ "contract": "diagnostic-fact-source", "version": 1 }],
              "requires": [{ "contract": "plugin", "version": 1 }]
            }
            """;

        var read = PluginManifestReader.TryRead(json, out var manifest);

        read.Should().BeTrue();
        manifest.Should().BeEquivalentTo(new PluginManifest(
            "diagnostics-clock",
            "0.1.0-alpha.3",
            "Voyager.Plugins.DiagnosticsClock.dll",
            "index.js",
            [new ContractReference("diagnostic-fact-source", 1)],
            [new ContractReference("plugin", 1)]));
    }

    [TestMethod]
    // The format used before contracts (an "sdk" field, plain strings in provides), no base contract, or an entry
    // that is not an object with a lower-case identifier and a whole version from 1.
    [DataRow("""{ "id": "a", "version": "1.0.0", "sdk": "0.1", "dotnet": { "assembly": "A.dll" }, "provides": [{ "contract": "diagnostic-fact-source", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": ["diagnostic-fact-source"], "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": { "contract": "diagnostic-fact-source", "version": 1 }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin" }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": "1" }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 0 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1.5 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "Plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1.0 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1e0 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": -1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 2147483648 }] }""")]
    // A key written twice: two readers could each keep a different one.
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }], "requires": [{ "contract": "plugin", "version": 2 }] }""")]
    // A name that Windows gives to a device, as the identifier of the plugin.
    [DataRow("""{ "id": "con", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    // A contract named twice, or the base contract provided instead of required.
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }, { "contract": "plugin", "version": 2 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": [{ "contract": "diagnostic-fact-source", "version": 1 }, { "contract": "diagnostic-fact-source", "version": 2 }], "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": [{ "contract": "plugin", "version": 1 }], "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    public void TryRead_ContractsInAnInvalidFormat_AreRefused(string json)
    {
        PluginManifestReader.TryRead(json, out var manifest).Should().BeFalse();

        manifest.Should().BeNull();
    }

    [TestMethod]
    public void TryRead_ContractThatTheApplicationDoesNotKnow_IsReadAsWritten()
    {
        // Whether the application knows a contract is not the reader's business: an unknown one is a reason of its own.
        const string json = """{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": [{ "contract": "future-point", "version": 3 }], "requires": [{ "contract": "plugin", "version": 9 }] }""";

        PluginManifestReader.TryRead(json, out var manifest).Should().BeTrue();

        manifest!.Provides.Should().Equal(new ContractReference("future-point", 3));
        manifest.Requires.Should().Equal(new ContractReference("plugin", 9));
    }

    [TestMethod]
    public void TryRead_MoreContractsThanTheLimit_IsRefused()
    {
        var provides = string.Join(", ", Enumerable.Range(1, 33).Select(index => $$"""{ "contract": "point-{{index}}", "version": 1 }"""));
        var json = $$"""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": [{{provides}}], "requires": [{ "contract": "plugin", "version": 1 }] }""";

        PluginManifestReader.TryRead(json, out var manifest).Should().BeFalse();

        manifest.Should().BeNull();
    }

    [TestMethod]
    public void TryRead_PluginWithOnlyAnInterfacePart_IsValid()
    {
        const string json = """{ "id": "view-only", "version": "1.2.3", "ui": { "module": "views/main.mjs" }, "requires": [{ "contract": "plugin", "version": 1 }] }""";

        PluginManifestReader.TryRead(json, out var manifest).Should().BeTrue();

        manifest!.DotnetAssembly.Should().BeNull();
        manifest.UiModule.Should().Be("views/main.mjs");
        manifest.Provides.Should().BeEmpty();
    }

    [TestMethod]
    public void TryRead_SameContractProvidedAndRequired_IsReadAsWritten()
    {
        const string json = """{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "provides": [{ "contract": "diagnostic-fact-source", "version": 1 }], "requires": [{ "contract": "plugin", "version": 1 }, { "contract": "diagnostic-fact-source", "version": 1 }] }""";

        PluginManifestReader.TryRead(json, out var manifest).Should().BeTrue();

        manifest!.Provides.Should().Equal(new ContractReference("diagnostic-fact-source", 1));
        manifest.Requires.Should().Equal(new ContractReference("plugin", 1), new ContractReference("diagnostic-fact-source", 1));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("""{ "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "Upper-Case", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "two--hyphens", "version": "1.0.0", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "one", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    // Digits of other scripts are digits for some patterns, never for a version.
    [DataRow("""{ "id": "a", "version": "１.０.０", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.٣", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0-rc.１", "dotnet": { "assembly": "A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": {}, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "sub/A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "dotnet": { "assembly": "..\\A.dll" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "ui": { "module": "../outside.js" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "ui": { "module": "/absolute.js" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "ui": { "module": "style.css" }, "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    [DataRow("""{ "id": "a", "version": "1.0.0", "ui": "index.js", "requires": [{ "contract": "plugin", "version": 1 }] }""")]
    // A newline at the end must not slip through the end-of-line anchor of a pattern.
    [DataRow("{ \"id\": \"a\\n\", \"version\": \"1.0.0\", \"dotnet\": { \"assembly\": \"A.dll\" }, \"requires\": [{ \"contract\": \"plugin\", \"version\": 1 }] }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\\n\", \"dotnet\": { \"assembly\": \"A.dll\" }, \"requires\": [{ \"contract\": \"plugin\", \"version\": 1 }] }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\", \"dotnet\": { \"assembly\": \"A.dll\\n\" }, \"requires\": [{ \"contract\": \"plugin\", \"version\": 1 }] }")]
    [DataRow("{ \"id\": \"a\", \"version\": \"1.0.0\", \"ui\": { \"module\": \"index.js\\n\" }, \"requires\": [{ \"contract\": \"plugin\", \"version\": 1 }] }")]
    public void TryRead_UnreadableOrIncompleteManifest_IsRefused(string json)
    {
        PluginManifestReader.TryRead(json, out var manifest).Should().BeFalse();

        manifest.Should().BeNull();
    }

    #endregion Public Methods
}
