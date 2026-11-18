using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class AddTravelerToTripTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_OwnTraveler_IsAddedToTheTrip()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, trip.Id, traveler.Id);

        result.ShouldSucceed().Travelers.Should().Equal(traveler.Id);
        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(traveler.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTravelersAfterTheProfile_FollowInTheOrderOfAddition()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());
        var travelerA = _world.AddTravelerOf(_world.Me, "Traveler A");
        var childC = _world.AddTravelerOf(_world.Me, "Child C");
        var trip = _world.AddTripOfMyGroup(profile.Id);

        await Execute(_world.Me, trip.Id, childC.Id);
        await Execute(_world.Me, trip.Id, travelerA.Id);

        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(profile.Id, childC.Id, travelerA.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerAlreadyInTheTrip_StaysThereOnlyOnce()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.Me, trip.Id, traveler.Id);

        result.ShouldSucceed().Travelers.Should().Equal(traveler.Id);
        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(traveler.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameTravelerAddedTwiceInARow_FiguresOnce()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup();

        await Execute(_world.Me, trip.Id, traveler.Id);
        await Execute(_world.Me, trip.Id, traveler.Id);

        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(traveler.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberAddsTheirOwnTravelerToATripOfAnotherMember_Succeeds()
    {
        var theirs = _world.AddTravelerOf(_world.MemberB, "Traveler B");
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberB, trip.Id, theirs.Id);

        result.ShouldSucceed().Travelers.Should().Equal(theirs.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerOfAnotherMember_ReceivesTravelerNotFoundAndTheTripIsUnchanged()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberB, trip.Id, mine.Id);

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
        _world.Trips.Find(trip.Id)!.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTraveler_ReceivesTravelerNotFound()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, trip.Id, TravelerId.New());

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfAnotherGroup_ReceivesTripNotFoundEvenWithTheirOwnTraveler()
    {
        var theirs = _world.AddTravelerOf(_world.MemberC, "Traveler C");
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberC, trip.Id, theirs.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
        _world.Trips.Find(trip.Id)!.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTrip_ReceivesTripNotFound()
    {
        var traveler = _world.AddTravelerOf(_world.Me);

        var result = await Execute(_world.Me, TripId.New(), traveler.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameTravelerInTwoTrips_IsAddedToEach()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var first = _world.AddTripOfMyGroup();
        var second = _world.AddTripOfMyGroup();

        await Execute(_world.Me, first.Id, traveler.Id);
        await Execute(_world.Me, second.Id, traveler.Id);

        _world.Trips.Find(first.Id)!.Includes(traveler.Id).Should().BeTrue();
        _world.Trips.Find(second.Id)!.Includes(traveler.Id).Should().BeTrue();
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Trip>> Execute(UserId caller, TripId trip, TravelerId traveler) =>
        new AddTravelerToTrip(_world.Trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new AddTravelerToTrip.Command(caller, trip, traveler), CancellationToken.None);

    #endregion Private Methods
}
