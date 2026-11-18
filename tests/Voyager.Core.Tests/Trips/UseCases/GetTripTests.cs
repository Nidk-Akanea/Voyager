using AwesomeAssertions;

using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class GetTripTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_TripWithTravelers_ListsThemInTheOrderTheyWereAdded()
    {
        var first = _world.AddTravelerOf(_world.Me, "Member A");
        var second = _world.AddTravelerOf(_world.Me, "Traveler A");
        var third = _world.AddTravelerOf(_world.Me, "Child C");
        var trip = _world.AddTripOfMyGroup(third.Id, first.Id, second.Id);

        var result = await Execute(_world.Me, trip.Id);

        result.ShouldSucceed().Travelers.Select(traveler => traveler.Id).Should().Equal(third.Id, first.Id, second.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherMemberOfTheGroup_SeesEachTravelerInFull()
    {
        var traveler = _world.AddTraveler(new TravelerBuilder()
            .WithOwner(_world.Me)
            .WithName("Traveler A")
            .WithNickname("TA")
            .WithBirthDate(new LocalDate(1990, 3, 15))
            .WithAvatar(TestImages.Png())
            .WithDescription("Likes long walks."));
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var view = (await Execute(_world.MemberB, trip.Id)).ShouldSucceed();

        var seen = view.Travelers.Should().ContainSingle().Which;
        seen.Name.Should().Be("Traveler A");
        seen.Nickname.Should().Be("TA");
        seen.BirthDate.Should().Be(new LocalDate(1990, 3, 15));
        seen.Avatar.Should().NotBeNull();
        seen.Description.Should().Be("Likes long walks.");
    }

    [TestMethod]
    public async Task ExecuteAsync_TripWithTravelersOfTwoMembers_ShowsBothToEachMember()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        var theirs = _world.AddTravelerOf(_world.MemberB, "Traveler B");
        var trip = _world.AddTripOfMyGroup(mine.Id, theirs.Id);

        var view = (await Execute(_world.MemberB, trip.Id)).ShouldSucceed();

        view.Travelers.Select(traveler => traveler.Id).Should().Equal(mine.Id, theirs.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TripWithoutTraveler_HasAnEmptyList()
    {
        var trip = _world.AddTripOfMyGroup();

        var view = (await Execute(_world.Me, trip.Id)).ShouldSucceed();

        view.Trip.Id.Should().Be(trip.Id);
        view.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_TripAskedFor_CarriesNameAndCreator()
    {
        var trip = _world.AddTripOfMyGroup();

        var view = (await Execute(_world.MemberB, trip.Id)).ShouldSucceed();

        view.Trip.Name.Should().Be("Summer in Brittany");
        view.Trip.CreatorId.Should().Be(_world.Me);
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfAnotherGroup_ReceivesTripNotFound()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.MemberC, trip.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTrip_ReceivesTripNotFound()
    {
        var result = await Execute(_world.Me, TripId.New());

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_TripDeletedWhileAnotherMemberHadItOpen_ReceivesTripNotFound()
    {
        var trip = _world.AddTripOfMyGroup();
        await new DeleteTrip(_world.Trips, _world.Groups)
            .ExecuteAsync(new DeleteTrip.Command(_world.Me, trip.Id), CancellationToken.None);

        var result = await Execute(_world.MemberB, trip.Id);

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<TripView>> Execute(UserId caller, TripId id) =>
        new GetTrip(_world.Trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new GetTrip.Command(caller, id), CancellationToken.None);

    #endregion Private Methods
}
