using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class DeleteTravelerTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_OwnerDeletesAnOrdinaryTraveler_TheTravelerIsGone()
    {
        var traveler = _world.AddTravelerOf(_world.Me);

        var result = await Execute(_world.Me, traveler.Id);

        result.IsSuccess.Should().BeTrue();
        _world.Travelers.Find(traveler.Id).Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerInTwoTrips_IsRemovedFromBothAndTheTripsStay()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");
        var first = _world.AddTripOfMyGroup(traveler.Id);
        var second = _world.AddTripOfMyGroup(traveler.Id);

        await Execute(_world.Me, traveler.Id);

        _world.Trips.Find(first.Id)!.Travelers.Should().BeEmpty();
        _world.Trips.Find(second.Id)!.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerInATripWithOthers_TheOtherTravelersStayInOrder()
    {
        var before = _world.AddTravelerOf(_world.Me, "Child C");
        var deleted = _world.AddTravelerOf(_world.Me, "Traveler A");
        var after = _world.AddTravelerOf(_world.MemberB, "Traveler B");
        var trip = _world.AddTripOfMyGroup(before.Id, deleted.Id, after.Id);

        await Execute(_world.Me, deleted.Id);

        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(before.Id, after.Id);
        _world.Travelers.Find(before.Id).Should().NotBeNull();
        _world.Travelers.Find(after.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_DeletedTraveler_LeavesTheListOfMyTravelers()
    {
        var kept = _world.AddTravelerOf(_world.Me, "Child C");
        var deleted = _world.AddTravelerOf(_world.Me, "Traveler A");

        await Execute(_world.Me, deleted.Id);

        var mine = await new ListMyTravelers(_world.Travelers).ExecuteAsync(new ListMyTravelers.Command(_world.Me), CancellationToken.None);
        mine.Select(traveler => traveler.Id).Should().Equal(kept.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_Profile_CannotBeDeleted()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());

        var result = await Execute(_world.Me, profile.Id);

        result.ShouldFailWith(TripsErrors.ProfileCannotBeDeleted);
        _world.Travelers.Find(profile.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_ProfileInATrip_StaysInTheTripAfterTheRefusal()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());
        var trip = _world.AddTripOfMyGroup(profile.Id);

        await Execute(_world.Me, profile.Id);

        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(profile.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherUser_ReceivesTravelerNotFoundAndTheTravelerStays()
    {
        var traveler = _world.AddTravelerOf(_world.Me);

        var result = await Execute(_world.MemberB, traveler.Id);

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
        _world.Travelers.Find(traveler.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfTheSameGroupSeeingTheTravelerInATrip_CannotDeleteIt()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.MemberB, traveler.Id);

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
        _world.Trips.Find(trip.Id)!.Travelers.Should().Equal(traveler.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTraveler_ReceivesTravelerNotFound()
    {
        var result = await Execute(_world.Me, TravelerId.New());

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerAlreadyDeleted_ReceivesTravelerNotFound()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        await Execute(_world.Me, traveler.Id);

        var result = await Execute(_world.Me, traveler.Id);

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Unit>> Execute(UserId caller, TravelerId id) =>
        new DeleteTraveler(_world.Travelers).ExecuteAsync(new DeleteTraveler.Command(caller, id), CancellationToken.None);

    #endregion Private Methods
}
