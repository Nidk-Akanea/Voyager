using AwesomeAssertions;

using Voyager.Sdk;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class ContractSupportTests
{
    #region Fields

    private const string ITINERARY_SOURCE = "itinerary-source";

    private static readonly ContractSupport _contracts = new(
    [
        new ContractVersion(ContractVersions.PLUGIN, 1, ContractVersionState.Supported),
        new ContractVersion(ITINERARY_SOURCE, 1, ContractVersionState.Obsolete),
        new ContractVersion(ITINERARY_SOURCE, 2, ContractVersionState.Deprecated),
        new ContractVersion(ITINERARY_SOURCE, 3, ContractVersionState.Supported),
    ],
    [ContractVersions.PLUGIN, ITINERARY_SOURCE]);

    #endregion Fields

    #region Private Methods

    private static PluginManifest Manifest(IReadOnlyList<ContractReference> provides, IReadOnlyList<ContractReference> requires) =>
        new("fixture-itinerary", "1.0.0", "Voyager.PluginFixtures.Good.dll", UiModule: null, provides, requires);

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public void Describe_PluginThatProvidesNothing_ListsOnlyTheRequiredContracts()
    {
        var manifest = Manifest(provides: [], requires: [new ContractReference(ContractVersions.PLUGIN, 1)]);

        var uses = _contracts.Describe(manifest);

        uses.Should().Equal(new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    public void Describe_ProvidedAndRequiredContracts_ListsTheProvidedFirstThenTheRequiredWithTheirStates()
    {
        var manifest = Manifest(
            provides: [new ContractReference(ITINERARY_SOURCE, 2), new ContractReference("weather-forecast-source", 1)],
            requires: [new ContractReference(ContractVersions.PLUGIN, 1), new ContractReference(ITINERARY_SOURCE, 1)]);

        var uses = _contracts.Describe(manifest);

        uses.Should().Equal(
            new ContractUse(ITINERARY_SOURCE, 2, ContractRole.Provided, ContractUseState.Deprecated),
            new ContractUse("weather-forecast-source", 1, ContractRole.Provided, ContractUseState.Unknown),
            new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported),
            new ContractUse(ITINERARY_SOURCE, 1, ContractRole.Required, ContractUseState.Obsolete));
    }

    [TestMethod]
    public void Describe_ProvidedContractThatTheApplicationDoesNotProvide_KeepsItsState()
    {
        var contracts = new ContractSupport(
            [
                new ContractVersion(ContractVersions.PLUGIN, 1, ContractVersionState.Supported),
                new ContractVersion(ITINERARY_SOURCE, 1, ContractVersionState.Supported),
            ],
            [ContractVersions.PLUGIN]);
        var manifest = Manifest(
            provides: [new ContractReference(ITINERARY_SOURCE, 1)],
            requires: [new ContractReference(ContractVersions.PLUGIN, 1)]);

        var uses = contracts.Describe(manifest);

        uses.Should().Equal(
            new ContractUse(ITINERARY_SOURCE, 1, ContractRole.Provided, ContractUseState.Supported),
            new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    public void Describe_RequiredContractThatTheApplicationDoesNotProvide_IsUnknown()
    {
        var contracts = new ContractSupport(
            [
                new ContractVersion(ContractVersions.PLUGIN, 1, ContractVersionState.Supported),
                new ContractVersion(ITINERARY_SOURCE, 1, ContractVersionState.Supported),
            ],
            [ContractVersions.PLUGIN]);
        var manifest = Manifest(
            provides: [],
            requires: [new ContractReference(ContractVersions.PLUGIN, 1), new ContractReference(ITINERARY_SOURCE, 1)]);

        var uses = contracts.Describe(manifest);

        uses.Should().Equal(
            new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported),
            new ContractUse(ITINERARY_SOURCE, 1, ContractRole.Required, ContractUseState.Unknown));
    }

    [TestMethod]
    [DataRow(ContractUseState.Supported, true)]
    [DataRow(ContractUseState.Deprecated, true)]
    [DataRow(ContractUseState.Obsolete, false)]
    [DataRow(ContractUseState.Unknown, false)]
    public void IsAccepted_StateOfTheVersion_AcceptsOnlySupportedAndDeprecated(ContractUseState state, bool expected)
    {
        var accepted = ContractSupport.IsAccepted(state);

        accepted.Should().Be(expected);
    }

    [TestMethod]
    [DataRow(ContractVersions.PLUGIN)]
    [DataRow(ExtensionPoints.DIAGNOSTIC_FACT_SOURCE)]
    public void StateOf_Application_SupportsVersionOneOfTheContractsOfTheSdk(string contract)
    {
        var state = ContractSupport.Application.StateOf(contract, 1);

        state.Should().Be(ContractUseState.Supported);
    }

    [TestMethod]
    public void StateOf_ContractThatIsNotListed_IsUnknown()
    {
        var state = _contracts.StateOf("weather-forecast-source", 1);

        state.Should().Be(ContractUseState.Unknown);
    }

    [TestMethod]
    public void StateOf_EmptyTable_IsUnknown()
    {
        var contracts = new ContractSupport([], []);

        var state = contracts.StateOf(ContractVersions.PLUGIN, 1);

        state.Should().Be(ContractUseState.Unknown);
    }

    [TestMethod]
    public void StateOf_EveryStateOfTheSdk_HasItsOwnCounterpart()
    {
        var states = Enum.GetValues<ContractVersionState>();
        var support = new ContractSupport(states.Select((state, index) => new ContractVersion(ITINERARY_SOURCE, index + 1, state)), []);

        var uses = states.Select((_, index) => support.StateOf(ITINERARY_SOURCE, index + 1)).ToList();

        uses.Should().NotContain(ContractUseState.Unknown).And.OnlyHaveUniqueItems();
    }

    [TestMethod]
    [DataRow(3, ContractUseState.Supported)]
    [DataRow(2, ContractUseState.Deprecated)]
    [DataRow(1, ContractUseState.Obsolete)]
    [DataRow(4, ContractUseState.Unknown)]
    public void StateOf_VersionOfAKnownContract_IsTheStateItIsListedWith(int version, ContractUseState expected)
    {
        var state = _contracts.StateOf(ITINERARY_SOURCE, version);

        state.Should().Be(expected);
    }

    #endregion Public Methods
}
