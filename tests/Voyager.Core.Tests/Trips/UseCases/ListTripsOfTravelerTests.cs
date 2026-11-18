using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class ListTripsOfTravelerTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_TravelerInTwoTrips_ListsBothInTheOrderTheyWereCreated()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var first = _world.AddTripOfMyGroup(traveler.Id);
        _world.AddTripOfMyGroup();
        var third = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.Me, traveler.Id);

        result.ShouldSucceed().Select(trip => trip.Id).Should().Equal(first.Id, third.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerInNoTrip_IsEmpty()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, traveler.Id);

        result.ShouldSucceed().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_TripOfAGroupTheCallerIsNotIn_IsNotNamed()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var visible = _world.AddTripOfMyGroup(traveler.Id);
        var former = GroupId.New();
        _world.Groups.Add(_world.Me, former);
        _world.AddTrip(new TripBuilder().WithGroup(former).WithCreator(_world.Me).WithName("Hidden trip").WithTravelers(traveler.Id));
        _world.Groups.Remove(_world.Me, former);

        var result = await Execute(_world.Me, traveler.Id);

        result.ShouldSucceed().Select(trip => trip.Id).Should().Equal(visible.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerOfAnotherUser_ReceivesTravelerNotFound()
    {
        var theirs = _world.AddTravelerOf(_world.MemberB);
        _world.AddTripOfMyGroup(theirs.Id);

        var result = await Execute(_world.Me, theirs.Id);

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTraveler_ReceivesTravelerNotFound()
    {
        var result = await Execute(_world.Me, TravelerId.New());

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<IReadOnlyList<Trip>>> Execute(UserId caller, TravelerId traveler) =>
        new ListTripsOfTraveler(_world.Trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new ListTripsOfTraveler.Command(caller, traveler), CancellationToken.None);

    #endregion Private Methods
}
