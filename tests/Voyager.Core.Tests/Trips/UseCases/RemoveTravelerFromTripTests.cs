using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class RemoveTravelerFromTripTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_TravelerInTheTrip_IsRemovedAndStaysAmongMyTravelers()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.Me, trip.Id, traveler.Id);

        result.ShouldSucceed().Travelers.Should().BeEmpty();
        _world.Trips.Find(trip.Id)!.Travelers.Should().BeEmpty();
        _world.Travelers.Find(traveler.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerInTheMiddle_LeavesTheOthersInOrder()
    {
        var first = _world.AddTravelerOf(_world.Me, "Member A");
        var second = _world.AddTravelerOf(_world.Me, "Traveler A");
        var third = _world.AddTravelerOf(_world.Me, "Child C");
        var trip = _world.AddTripOfMyGroup(first.Id, second.Id, third.Id);

        await Execute(_world.Me, trip.Id, second.Id);

        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(first.Id, third.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_CreatorRemovesTheirOwnProfile_LeavesATripWithoutTraveler()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());
        var trip = _world.AddTripOfMyGroup(profile.Id);

        var result = await Execute(_world.Me, trip.Id, profile.Id);

        result.ShouldSucceed().Travelers.Should().BeEmpty();
        _world.Trips.Find(trip.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberRemovesATravelerThatBelongsToAnotherMember_Succeeds()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        var trip = _world.AddTripOfMyGroup(mine.Id);

        var result = await Execute(_world.MemberB, trip.Id, mine.Id);

        result.ShouldSucceed().Travelers.Should().BeEmpty();
        _world.Travelers.Find(mine.Id).Should().NotBeNull();
        _world.Travelers.Find(mine.Id)!.OwnerId.Should().Be(_world.Me);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerNotInTheTrip_SucceedsAndChangesNothing()
    {
        var inside = _world.AddTravelerOf(_world.Me, "Traveler A");
        var outside = _world.AddTravelerOf(_world.Me, "Child C");
        var trip = _world.AddTripOfMyGroup(inside.Id);

        var result = await Execute(_world.Me, trip.Id, outside.Id);

        result.ShouldSucceed().Travelers.Should().Equal(inside.Id);
        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(inside.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerRemovedTwice_SucceedsBothTimes()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup(traveler.Id);
        await Execute(_world.Me, trip.Id, traveler.Id);

        var second = await Execute(_world.MemberB, trip.Id, traveler.Id);

        second.ShouldSucceed().Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerRemovedFromOneTrip_StaysInTheOthers()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var first = _world.AddTripOfMyGroup(traveler.Id);
        var second = _world.AddTripOfMyGroup(traveler.Id);

        await Execute(_world.Me, first.Id, traveler.Id);

        _world.Trips.Find(first.Id)!.Includes(traveler.Id).Should().BeFalse();
        _world.Trips.Find(second.Id)!.Includes(traveler.Id).Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfAnotherGroup_ReceivesTripNotFoundAndTheTripIsUnchanged()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.MemberC, trip.Id, traveler.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(traveler.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTrip_ReceivesTripNotFound()
    {
        var result = await Execute(_world.Me, TripId.New(), TravelerId.New());

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Trip>> Execute(UserId caller, TripId trip, TravelerId traveler) =>
        new RemoveTravelerFromTrip(_world.Trips, _world.Groups)
            .ExecuteAsync(new RemoveTravelerFromTrip.Command(caller, trip, traveler), CancellationToken.None);

    #endregion Private Methods
}
