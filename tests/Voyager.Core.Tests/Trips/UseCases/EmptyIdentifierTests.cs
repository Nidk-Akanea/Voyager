using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

/// <summary>
/// The identifier of a creation comes from the client, which could send the empty one: it would be the same for everyone.
/// </summary>
[TestClass]
public sealed class EmptyIdentifierTests
{
    #region Fields

    private static readonly Error _expected = TripsErrors.Invalid(TripsErrors.ID_FIELD, TripsErrors.REQUIRED);

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task CreateMyProfile_EmptyIdentifier_IsRefused()
    {
        var result = await new CreateMyProfile(_world.Travelers, _world.Clock).ExecuteAsync(
            new CreateMyProfile.Command(_world.Me, new TravelerId(Guid.Empty), TestDetails.Of(), AvatarChange.Keep),
            CancellationToken.None);

        result.ShouldFailWith(_expected);
        _world.Travelers.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CreateTrip_EmptyIdentifier_IsRefused()
    {
        var result = await new CreateTrip(_world.Trips, _world.Travelers, _world.Groups).ExecuteAsync(
            new CreateTrip.Command(_world.Me, new TripId(Guid.Empty), _world.Group, "Été en Bretagne"),
            CancellationToken.None);

        result.ShouldFailWith(_expected);
        _world.Trips.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task DeclareTraveler_EmptyIdentifier_IsRefused()
    {
        var result = await new DeclareTraveler(_world.Travelers, _world.Clock).ExecuteAsync(
            new DeclareTraveler.Command(_world.Me, new TravelerId(Guid.Empty), TestDetails.Of(), AvatarChange.Keep),
            CancellationToken.None);

        result.ShouldFailWith(_expected);
        _world.Travelers.All.Should().BeEmpty();
    }

    #endregion Public Methods
}
