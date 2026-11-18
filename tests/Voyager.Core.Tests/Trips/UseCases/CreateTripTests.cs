using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class CreateTripTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_MemberWithAProfile_CreatesTheTripWithTheProfileAsOnlyTraveler()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());
        var id = TripId.New();

        var result = await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        var trip = result.ShouldSucceed();
        trip.Id.Should().Be(id);
        trip.GroupId.Should().Be(_world.Group);
        trip.CreatorId.Should().Be(_world.Me);
        trip.Name.Should().Be("Summer in Brittany");
        trip.Travelers.Should().Equal(profile.Id);
        _world.Trips.Find(id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberWithoutProfile_CreatesATripWithNoTraveler()
    {
        var result = await Execute(_world.Me, TripId.New(), _world.Group, "Summer in Brittany");

        result.ShouldSucceed().Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberWithOrdinaryTravelersButNoProfile_AddsNobody()
    {
        _world.AddTravelerOf(_world.Me, "Child C");

        var result = await Execute(_world.Me, TripId.New(), _world.Group, "Summer in Brittany");

        result.ShouldSucceed().Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_ProfileOfAnotherMember_IsNotAddedToMyTrip()
    {
        _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());

        var result = await Execute(_world.MemberB, TripId.New(), _world.Group, "Summer in Brittany");

        result.ShouldSucceed().Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_TripCreated_AppearsInTheListOfTheGroupForAnotherMember()
    {
        var id = TripId.New();
        await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        var list = await new ListTrips(_world.Trips, _world.Groups)
            .ExecuteAsync(new ListTrips.Command(_world.MemberB, _world.Group), CancellationToken.None);

        list.ShouldSucceed().Select(trip => trip.Id).Should().Equal(id);
    }

    [TestMethod]
    public async Task ExecuteAsync_CallerNotInTheGroup_ReceivesNotAMemberAndNothingIsKept()
    {
        var result = await Execute(_world.MemberC, TripId.New(), _world.Group, "Summer in Brittany");

        result.ShouldFailWith(TripsErrors.NotAMember);
        _world.Trips.All.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task ExecuteAsync_MissingOrBlankName_IsRefusedAndNothingIsKept(string? name)
    {
        var result = await Execute(_world.Me, TripId.New(), _world.Group, name);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
        _world.Trips.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_NameOf101Characters_IsRefusedAsTooLong()
    {
        var result = await Execute(_world.Me, TripId.New(), _world.Group, new string('a', 101));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
        _world.Trips.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_NameOf100Characters_IsAccepted()
    {
        var result = await Execute(_world.Me, TripId.New(), _world.Group, new string('a', 100));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_NameWithSpacesAndBidirectionalCharacters_IsKeptCleaned()
    {
        var result = await Execute(_world.Me, TripId.New(), _world.Group, "  Summer ‮in Brittany ");

        result.ShouldSucceed().Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTripsWithTheSameName_AreBothKept()
    {
        await Execute(_world.Me, TripId.New(), _world.Group, "Summer in Brittany");

        var second = await Execute(_world.Me, TripId.New(), _world.Group, "Summer in Brittany");

        second.IsSuccess.Should().BeTrue();
        _world.Trips.All.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameCreationSentTwice_SucceedsWithASingleTrip()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());
        var id = TripId.New();
        await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        var second = await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        second.ShouldSucceed().Id.Should().Be(id);
        _world.Trips.All.Should().ContainSingle();
        _world.Trips.Find(id)!.Travelers.Should().Equal(profile.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierWithAnotherName_IsRefusedAsIdAlreadyUsed()
    {
        var id = TripId.New();
        await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        var second = await Execute(_world.Me, id, _world.Group, "Winter in Lapland");

        second.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Trips.All.Should().ContainSingle().Which.Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierAndNameFromAnotherCreator_IsRefusedAsIdAlreadyUsed()
    {
        var id = TripId.New();
        await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        var second = await Execute(_world.MemberB, id, _world.Group, "Summer in Brittany");

        second.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Trips.Find(id)!.CreatorId.Should().Be(_world.Me);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierAndNameInAnotherGroup_IsRefusedAsIdAlreadyUsed()
    {
        var id = TripId.New();
        await Execute(_world.Me, id, _world.Group, "Summer in Brittany");
        var otherGroup = GroupId.New();
        _world.Groups.Add(_world.Me, otherGroup);

        var second = await Execute(_world.Me, id, otherGroup, "Summer in Brittany");

        second.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Trips.Find(id)!.GroupId.Should().Be(_world.Group);
    }

    [TestMethod]
    public async Task ExecuteAsync_IdentifierOfATripOfAnotherGroupFromANonMember_ReceivesNotAMember()
    {
        var id = TripId.New();
        await Execute(_world.Me, id, _world.Group, "Summer in Brittany");

        var attempt = await Execute(_world.MemberC, id, _world.Group, "Summer in Brittany");

        attempt.ShouldFailWith(TripsErrors.NotAMember);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Trip>> Execute(UserId caller, TripId id, GroupId group, string? name) =>
        new CreateTrip(_world.Trips, _world.Travelers, _world.Groups)
            .ExecuteAsync(new CreateTrip.Command(caller, id, group, name), CancellationToken.None);

    #endregion Private Methods
}
