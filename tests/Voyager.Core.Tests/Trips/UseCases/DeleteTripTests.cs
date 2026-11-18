using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class DeleteTripTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_Creator_DeletesTheTripAndItsTravelersStay()
    {
        var first = _world.AddTravelerOf(_world.Me, "Traveler A");
        var second = _world.AddTravelerOf(_world.Me, "Child C");
        var trip = _world.AddTripOfMyGroup(first.Id, second.Id);

        var result = await Execute(_world.Me, trip.Id);

        result.IsSuccess.Should().BeTrue();
        _world.Trips.Find(trip.Id).Should().BeNull();
        _world.Travelers.Find(first.Id).Should().NotBeNull();
        _world.Travelers.Find(second.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_Creator_DeletesOnlyThatTrip()
    {
        var other = _world.AddTripOfMyGroup();
        var trip = _world.AddTripOfMyGroup();

        await Execute(_world.Me, trip.Id);

        _world.Trips.All.Should().ContainSingle().Which.Id.Should().Be(other.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberWhoIsNotTheCreator_ReceivesNotTheCreatorAndTheTripStays()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberB, trip.Id);

        result.ShouldFailWith(TripsErrors.NotTheCreator);
        _world.Trips.Find(trip.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfAnotherGroup_ReceivesTripNotFoundAndTheTripStays()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberC, trip.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
        _world.Trips.Find(trip.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_CreatorWhoLeftTheGroup_ReceivesTripNotFound()
    {
        var trip = _world.AddTripOfMyGroup();
        _world.Groups.Remove(_world.Me, _world.Group);

        var result = await Execute(_world.Me, trip.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
        _world.Trips.Find(trip.Id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTrip_ReceivesTripNotFound()
    {
        var result = await Execute(_world.Me, TripId.New());

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_TripAlreadyDeleted_ReceivesTripNotFound()
    {
        var trip = _world.AddTripOfMyGroup();
        await Execute(_world.Me, trip.Id);

        var result = await Execute(_world.Me, trip.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Unit>> Execute(UserId caller, TripId id) =>
        new DeleteTrip(_world.Trips, _world.Groups).ExecuteAsync(new DeleteTrip.Command(caller, id), CancellationToken.None);

    #endregion Private Methods
}
