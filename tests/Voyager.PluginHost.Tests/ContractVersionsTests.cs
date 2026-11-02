using System.Reflection;

using AwesomeAssertions;

using Voyager.Sdk;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class ContractVersionsTests
{
    #region Public Methods

    [TestMethod]
    public void All_Sdk_ContainsTheBaseContract()
    {
        var contracts = ContractVersions.All.Select(version => version.Contract);

        contracts.Should().Contain(ContractVersions.PLUGIN);
    }

    [TestMethod]
    public void All_Sdk_GivesAtLeastOneVersionToEveryExtensionPoint()
    {
        var extensionPoints = typeof(ExtensionPoints)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        var contracts = ContractVersions.All.Select(version => version.Contract).ToHashSet(StringComparer.Ordinal);

        extensionPoints.Should().NotBeEmpty();
        extensionPoints.Should().AllSatisfy(extensionPoint => contracts.Should().Contain(extensionPoint));
    }

    [TestMethod]
    public void All_Sdk_ListsEachVersionOfEachContractOnce()
    {
        var keys = ContractVersions.All.Select(version => (version.Contract, version.Version));

        keys.Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void All_Sdk_NumbersEveryVersionFromOne()
    {
        var versions = ContractVersions.All.Select(version => version.Version);

        versions.Should().AllSatisfy(version => version.Should().BeGreaterThanOrEqualTo(1));
    }

    #endregion Public Methods
}
