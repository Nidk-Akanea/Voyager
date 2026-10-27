using AwesomeAssertions;

using Voyager.Sdk.Diagnostics;

namespace Voyager.Plugins.DiagnosticsRuntime.Tests;

[TestClass]
public sealed class RuntimeFactSourceTests
{
    [TestMethod]
    public async Task GetFactsAsync_Always_ReturnsProcessorsMemoryAndCollectionsAsNumbers()
    {
        var facts = await new RuntimeFactSource().GetFactsAsync(CancellationToken.None);

        facts.Select(fact => fact.LabelId).Should().Equal(
            "diagnostics-runtime-fact-processors",
            "diagnostics-runtime-fact-memory",
            "diagnostics-runtime-fact-collections");
        facts.Should().AllSatisfy(fact => fact.Value.Kind.Should().Be(DiagnosticFactKind.Number));
        facts[0].Value.Number.Should().Be(Environment.ProcessorCount);
        facts[1].Value.Number.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task GetFactsAsync_Always_PrefixesEveryLabelWithThePluginId()
    {
        var facts = await new RuntimeFactSource().GetFactsAsync(CancellationToken.None);

        facts.Should().AllSatisfy(fact => fact.LabelId.Should().StartWith("diagnostics-runtime-"));
    }
}
