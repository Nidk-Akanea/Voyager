using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class ListTripsTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_GroupWithTrips_ListsThemInTheOrderTheyWereCreated()
    {
        var first = _world.AddTripOfMyGroup();
        var second = _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, _world.Group);

        result.ShouldSucceed().Select(trip => trip.Id).Should().Equal(first.Id, second.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyMemberNotOnlyTheCreator_SeesTheTrips()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberB, _world.Group);

        result.ShouldSucceed().Select(other => other.Id).Should().Equal(trip.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_GroupWithoutTrip_IsEmpty()
    {
        var result = await Execute(_world.Me, _world.Group);

        result.ShouldSucceed().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_TripsOfAnotherGroup_AreNotListed()
    {
        var mine = _world.AddTripOfMyGroup();
        _world.AddTrip(new TripBuilder().WithGroup(_world.OtherGroup).WithCreator(_world.MemberC).WithName("Other trip"));

        var result = await Execute(_world.Me, _world.Group);

        result.ShouldSucceed().Select(trip => trip.Id).Should().Equal(mine.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfAnotherGroup_ReceivesNotAMemberAndSeesNoTrip()
    {
        _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberC, _world.Group);

        result.ShouldFailWith(TripsErrors.NotAMember);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<IReadOnlyList<Trip>>> Execute(UserId caller, GroupId group) =>
        new ListTrips(_world.Trips, _world.Groups).ExecuteAsync(new ListTrips.Command(caller, group), CancellationToken.None);

    #endregion Private Methods
}
