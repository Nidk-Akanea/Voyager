using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Trips;

[TestClass]
public sealed class TripTests
{
    #region Public Methods

    #region Creation

    [TestMethod]
    public void Create_ValidName_KeepsIdentityGroupAndCreator()
    {
        var id = TripId.New();
        var group = GroupId.New();
        var creator = UserId.New();

        var trip = Trip.Create(id, group, creator, "Summer in Brittany", creatorTraveler: null).ShouldSucceed();

        trip.Id.Should().Be(id);
        trip.GroupId.Should().Be(group);
        trip.CreatorId.Should().Be(creator);
        trip.Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public void Create_WithCreatorTraveler_HasThatTravelerAlone()
    {
        var traveler = TravelerId.New();

        var trip = Create("Summer in Brittany", traveler).ShouldSucceed();

        trip.Travelers.Should().Equal(traveler);
    }

    [TestMethod]
    public void Create_WithoutCreatorTraveler_HasNoTraveler()
    {
        var trip = Create("Summer in Brittany").ShouldSucceed();

        trip.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("‮\n")]
    public void Create_NameMissingOrBlank_IsRefusedAsRequired(string? name)
    {
        var result = Create(name);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
    }

    [TestMethod]
    public void Create_NameOf100Characters_IsAccepted()
    {
        var result = Create(new string('a', 100));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Create_NameOf101Characters_IsRefusedAsTooLong()
    {
        var result = Create(new string('a', 101));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public void Create_NameWithSpacesControlAndBidirectionalCharacters_IsCleaned()
    {
        var trip = Create("  Summer\n in ‮Brittany\t ").ShouldSucceed();

        trip.Name.Should().Be("Summer in Brittany");
    }

    #endregion Creation

    #region Rename

    [TestMethod]
    public void Rename_ValidName_KeepsIdentityCreatorGroupAndTravelers()
    {
        var first = TravelerId.New();
        var second = TravelerId.New();
        var trip = new TripBuilder().WithName("Summer in Brittany").WithTravelers(first, second).Build();

        var renamed = trip.Rename("Brittany 2027").ShouldSucceed();

        renamed.Name.Should().Be("Brittany 2027");
        renamed.Id.Should().Be(trip.Id);
        renamed.CreatorId.Should().Be(trip.CreatorId);
        renamed.GroupId.Should().Be(trip.GroupId);
        renamed.Travelers.Should().Equal(first, second);
    }

    [TestMethod]
    public void Rename_InvalidName_IsRefusedAndTheTripIsUnchanged()
    {
        var trip = new TripBuilder().WithName("Summer in Brittany").Build();

        var result = trip.Rename(new string('a', 101));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
        trip.Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public void Rename_BlankName_IsRefusedAsRequired()
    {
        var trip = new TripBuilder().Build();

        var result = trip.Rename("   ");

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
    }

    [TestMethod]
    public void Rename_NameWithSpaces_IsTrimmed()
    {
        var trip = new TripBuilder().Build();

        var renamed = trip.Rename("  Brittany 2027  ").ShouldSucceed();

        renamed.Name.Should().Be("Brittany 2027");
    }

    #endregion Rename

    #region Travelers

    [TestMethod]
    public void WithTraveler_NewTraveler_IsAddedAfterTheOthers()
    {
        var first = TravelerId.New();
        var second = TravelerId.New();
        var trip = new TripBuilder().WithTravelers(first).Build();

        var updated = trip.WithTraveler(second);

        updated.Travelers.Should().Equal(first, second);
    }

    [TestMethod]
    public void WithTraveler_AddedOneAfterTheOther_KeepsTheOrderOfAddition()
    {
        var first = TravelerId.New();
        var second = TravelerId.New();
        var third = TravelerId.New();
        var trip = new TripBuilder().Build();

        var updated = trip.WithTraveler(third).WithTraveler(first).WithTraveler(second);

        updated.Travelers.Should().Equal(third, first, second);
    }

    [TestMethod]
    public void WithTraveler_TravelerAlreadyThere_ReturnsTheSameTrip()
    {
        var traveler = TravelerId.New();
        var trip = new TripBuilder().WithTravelers(traveler).Build();

        var updated = trip.WithTraveler(traveler);

        updated.Should().BeSameAs(trip);
        updated.Travelers.Should().Equal(traveler);
    }

    [TestMethod]
    public void WithTraveler_NewTraveler_LeavesTheOriginalTripUnchanged()
    {
        var trip = new TripBuilder().Build();

        _ = trip.WithTraveler(TravelerId.New());

        trip.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public void WithoutTraveler_TravelerThere_RemovesItAndKeepsTheOthersInOrder()
    {
        var first = TravelerId.New();
        var second = TravelerId.New();
        var third = TravelerId.New();
        var trip = new TripBuilder().WithTravelers(first, second, third).Build();

        var updated = trip.WithoutTraveler(second);

        updated.Travelers.Should().Equal(first, third);
        trip.Travelers.Should().Equal(first, second, third);
    }

    [TestMethod]
    public void WithoutTraveler_TravelerNotThere_ReturnsTheSameTrip()
    {
        var trip = new TripBuilder().WithTravelers(TravelerId.New()).Build();

        var updated = trip.WithoutTraveler(TravelerId.New());

        updated.Should().BeSameAs(trip);
    }

    [TestMethod]
    public void WithoutTraveler_LastTraveler_LeavesATripWithNoTraveler()
    {
        var traveler = TravelerId.New();
        var trip = new TripBuilder().WithTravelers(traveler).Build();

        var updated = trip.WithoutTraveler(traveler);

        updated.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public void Includes_TravelerThereOrNot_TellsWhich()
    {
        var there = TravelerId.New();
        var trip = new TripBuilder().WithTravelers(there).Build();

        trip.Includes(there).Should().BeTrue();
        trip.Includes(TravelerId.New()).Should().BeFalse();
    }

    [TestMethod]
    public void Restore_StoredTrip_KeepsEverythingIncludingTheOrder()
    {
        var first = TravelerId.New();
        var second = TravelerId.New();
        var id = TripId.New();
        var group = GroupId.New();
        var creator = UserId.New();

        var trip = Trip.Restore(id, group, creator, "Summer in Brittany", [second, first]);

        trip.Id.Should().Be(id);
        trip.GroupId.Should().Be(group);
        trip.CreatorId.Should().Be(creator);
        trip.Travelers.Should().Equal(second, first);
    }

    #endregion Travelers

    #endregion Public Methods

    #region Private Methods

    private static Result<Trip> Create(string? name, TravelerId? creatorTraveler = null) =>
        Trip.Create(TripId.New(), GroupId.New(), UserId.New(), name, creatorTraveler);

    #endregion Private Methods
}
