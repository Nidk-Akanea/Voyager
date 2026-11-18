using AwesomeAssertions;

using Voyager.Core.Tests.Support;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

/// <summary>
/// Two members work on the same trip at the same time: a change of one never undoes a change of the other (spec of the
/// trip, rule 6). The other member changes the trip right after the use case read it.
/// </summary>
[TestClass]
public sealed class ConcurrentChangesTests
{
    #region Fields

    private readonly ChangingTripRepository _trips;
    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Constructors

    public ConcurrentChangesTests() => _trips = new ChangingTripRepository(_world.Trips);

    #endregion Public Constructors

    #region Public Methods

    [TestMethod]
    public async Task AddTravelerToTrip_AnotherMemberAddsAnotherMeanwhile_BothStay()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        var theirs = _world.AddTravelerOf(_world.MemberB, "Child C");
        var trip = _world.AddTripOfMyGroup();
        _trips.AfterNextFind = () => _world.Trips.AddTravelerAsync(trip.Id, theirs.Id, CancellationToken.None);

        var result = await new AddTravelerToTrip(_trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new AddTravelerToTrip.Command(_world.Me, trip.Id, mine.Id), CancellationToken.None);

        result.ShouldSucceed().Travelers.Should().Equal(theirs.Id, mine.Id);
    }

    [TestMethod]
    public async Task AddTravelerToTrip_SameTravelerAddedMeanwhile_FiguresOnce()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        var trip = _world.AddTripOfMyGroup();
        _trips.AfterNextFind = () => _world.Trips.AddTravelerAsync(trip.Id, mine.Id, CancellationToken.None);

        var result = await new AddTravelerToTrip(_trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new AddTravelerToTrip.Command(_world.Me, trip.Id, mine.Id), CancellationToken.None);

        result.ShouldSucceed().Travelers.Should().Equal(mine.Id);
    }

    [TestMethod]
    public async Task RemoveTravelerFromTrip_AnotherMemberAddsMeanwhile_KeepsTheAddition()
    {
        var leaving = _world.AddTravelerOf(_world.Me, "Traveler A");
        var arriving = _world.AddTravelerOf(_world.MemberB, "Child C");
        var trip = _world.AddTripOfMyGroup(leaving.Id);
        _trips.AfterNextFind = () => _world.Trips.AddTravelerAsync(trip.Id, arriving.Id, CancellationToken.None);

        var result = await new RemoveTravelerFromTrip(_trips, _world.Groups)
            .ExecuteAsync(new RemoveTravelerFromTrip.Command(_world.Me, trip.Id, leaving.Id), CancellationToken.None);

        result.ShouldSucceed().Travelers.Should().Equal(arriving.Id);
    }

    [TestMethod]
    public async Task RenameTrip_AnotherMemberAddsATravelerMeanwhile_TakesTheNameAndKeepsTheTraveler()
    {
        var theirs = _world.AddTravelerOf(_world.MemberB, "Child C");
        var trip = _world.AddTripOfMyGroup();
        _trips.AfterNextFind = () => _world.Trips.AddTravelerAsync(trip.Id, theirs.Id, CancellationToken.None);

        var result = await new RenameTrip(_trips, _world.Groups)
            .ExecuteAsync(new RenameTrip.Command(_world.Me, trip.Id, "Bretagne 2027"), CancellationToken.None);

        var renamed = result.ShouldSucceed();
        renamed.Name.Should().Be("Bretagne 2027");
        renamed.Travelers.Should().Equal(theirs.Id);
    }

    [TestMethod]
    public async Task AddTravelerToTrip_TripDeletedMeanwhile_IsNotFound()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        var trip = _world.AddTripOfMyGroup();
        _trips.AfterNextFind = () => _world.Trips.DeleteAsync(trip.Id, CancellationToken.None);

        var result = await new AddTravelerToTrip(_trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new AddTravelerToTrip.Command(_world.Me, trip.Id, mine.Id), CancellationToken.None);

        result.ShouldFailWith(Voyager.Core.Trips.TripsErrors.TripNotFound);
    }

    #endregion Public Methods
}
