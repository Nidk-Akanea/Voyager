using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Trips;
using Voyager.Persistence.Tests.Support;

namespace Voyager.Persistence.Tests.Trips;

[TestClass]
public sealed class TripRepositoryTests
{
    private static TestDatabase _database = null!;

    [ClassCleanup]
    public static async Task CleanupAsync() => await _database.DisposeAsync();

    [ClassInitialize]
    public static async Task InitializeAsync(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _database = await TestDatabase.CreateAsync();
    }

    [TestMethod]
    public async Task AddTraveler_AfterThoseAlreadyIn_KeepsTheOrderOfTheAdditions()
    {
        // Identifiers made in one order, travelers added in another: the order of the additions wins.
        var first = await SeedTravelerAsync("Traveler A");
        var second = await SeedTravelerAsync("Member B");
        var third = await SeedTravelerAsync("Child C");
        var trip = await _database.SeedAsync(new TripBuilder().Build());

        await _database.Trips.AddTravelerAsync(trip.Id, third.Id, CancellationToken.None);
        await _database.Trips.AddTravelerAsync(trip.Id, first.Id, CancellationToken.None);
        await _database.Trips.AddTravelerAsync(trip.Id, second.Id, CancellationToken.None);

        (await _database.TravelersOfAsync(trip.Id)).Should().Equal(third.Id, first.Id, second.Id);
    }

    [TestMethod]
    public async Task AddTraveler_SameTravelerTwice_KeepsItOnceWhereItWas()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var memberB = await SeedTravelerAsync("Member B");
        var trip = await _database.SeedAsync(new TripBuilder().WithTravelers(travelerA.Id).Build());

        await _database.Trips.AddTravelerAsync(trip.Id, memberB.Id, CancellationToken.None);
        await _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None);
        await _database.Trips.AddTravelerAsync(trip.Id, memberB.Id, CancellationToken.None);

        (await _database.TravelersOfAsync(trip.Id)).Should().Equal(travelerA.Id, memberB.Id);
    }

    [TestMethod]
    public async Task AddTraveler_TravelerAlreadyInTheTrip_DoesNotThrow()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var trip = await _database.SeedAsync(new TripBuilder().WithTravelers(travelerA.Id).Build());

        var add = async () => await _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None);

        await add.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task AddTraveler_UnknownTrip_DoesNothingAndDoesNotThrow()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var unknownTrip = new TripId(Guid.CreateVersion7());

        var add = async () => await _database.Trips.AddTravelerAsync(unknownTrip, travelerA.Id, CancellationToken.None);

        await add.Should().NotThrowAsync();
        (await _database.Trips.ListByTravelerAsync(travelerA.Id, CancellationToken.None)).Should().BeEmpty();
    }

    [TestMethod]
    public async Task AddTraveler_UnknownTraveler_DoesNothingAndDoesNotThrow()
    {
        var trip = await _database.SeedAsync(new TripBuilder().Build());

        var add = async () => await _database.Trips.AddTravelerAsync(trip.Id, TravelerId.New(), CancellationToken.None);

        await add.Should().NotThrowAsync();
        (await _database.TravelersOfAsync(trip.Id)).Should().BeEmpty();
    }

    [TestMethod]
    public async Task AddTraveler_TravelerDeletedMeanwhile_DoesNothingAndDoesNotThrow()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var trip = await _database.SeedAsync(new TripBuilder().Build());
        await _database.Travelers.DeleteAsync(travelerA.Id, CancellationToken.None);

        var add = async () => await _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None);

        await add.Should().NotThrowAsync();
        (await _database.TravelersOfAsync(trip.Id)).Should().BeEmpty();
    }

    [TestMethod]
    public async Task AddTraveler_TripAlreadyDeleted_DoesNothingAndDoesNotThrow()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var trip = await _database.SeedAsync(new TripBuilder().Build());
        await _database.Trips.DeleteAsync(trip.Id, CancellationToken.None);

        var add = async () => await _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None);

        await add.Should().NotThrowAsync();
        (await _database.Trips.FindAsync(trip.Id, CancellationToken.None)).Should().BeNull();
    }

    [TestMethod]
    public async Task Delete_TripWithTravelers_RemovesItAndLeavesTheTravelers()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var childC = await SeedTravelerAsync("Child C");
        var trip = await _database.SeedAsync(new TripBuilder().WithTravelers(travelerA.Id, childC.Id).Build());

        await _database.Trips.DeleteAsync(trip.Id, CancellationToken.None);

        (await _database.Trips.FindAsync(trip.Id, CancellationToken.None)).Should().BeNull();
        (await _database.Travelers.FindAsync(travelerA.Id, CancellationToken.None)).Should().NotBeNull();
        (await _database.Travelers.FindAsync(childC.Id, CancellationToken.None)).Should().NotBeNull();
        (await _database.Trips.ListByTravelerAsync(travelerA.Id, CancellationToken.None)).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Delete_TripOnly_LeavesTheOtherTrips()
    {
        var group = new GroupId(Guid.CreateVersion7());
        var deleted = await _database.SeedAsync(new TripBuilder().InGroup(group).Build());
        var kept = await _database.SeedAsync(new TripBuilder().InGroup(group).WithName("Trip to Porto").Build());

        await _database.Trips.DeleteAsync(deleted.Id, CancellationToken.None);

        (await _database.Trips.ListByGroupAsync(group, CancellationToken.None)).Select(trip => trip.Id).Should().Equal(kept.Id);
    }

    [TestMethod]
    public async Task Delete_UnknownTrip_DoesNotThrow()
    {
        var delete = async () => await _database.Trips.DeleteAsync(new TripId(Guid.CreateVersion7()), CancellationToken.None);

        await delete.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task Find_UnknownTrip_ReturnsNull()
    {
        var found = await _database.Trips.FindAsync(new TripId(Guid.CreateVersion7()), CancellationToken.None);

        found.Should().BeNull();
    }

    [TestMethod]
    public async Task Find_ExistingTrip_ReturnsItsGroupCreatorNameAndTravelers()
    {
        var group = new GroupId(Guid.CreateVersion7());
        var creator = UserId.New();
        var travelerA = await SeedTravelerAsync("Traveler A");
        var trip = await _database.SeedAsync(
            new TripBuilder().InGroup(group).WithCreator(creator).WithName("Trip to Porto").WithTravelers(travelerA.Id).Build());

        var found = await _database.Trips.FindAsync(trip.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(trip.Id);
        found.GroupId.Should().Be(group);
        found.CreatorId.Should().Be(creator);
        found.Name.Should().Be("Trip to Porto");
        found.Travelers.Should().Equal(travelerA.Id);
    }

    [TestMethod]
    public async Task ListByGroup_SeveralTrips_ReturnsThemInTheOrderTheyWereCreated()
    {
        var group = new GroupId(Guid.CreateVersion7());

        // Identifiers made in one order, trips created in another: the order of the creations wins.
        var ids = new[] { new TripId(Guid.CreateVersion7()), new TripId(Guid.CreateVersion7()), new TripId(Guid.CreateVersion7()) };
        await _database.SeedAsync(new TripBuilder().InGroup(group).WithId(ids[2]).Build());
        await _database.SeedAsync(new TripBuilder().InGroup(group).WithId(ids[0]).Build());
        await _database.SeedAsync(new TripBuilder().InGroup(group).WithId(ids[1]).Build());

        var found = await _database.Trips.ListByGroupAsync(group, CancellationToken.None);

        found.Select(trip => trip.Id).Should().Equal(ids[2], ids[0], ids[1]);
    }

    [TestMethod]
    public async Task ListByGroup_TripsOfAnotherGroup_LeavesThemOut()
    {
        var group = new GroupId(Guid.CreateVersion7());
        var mine = await _database.SeedAsync(new TripBuilder().InGroup(group).Build());
        await _database.SeedAsync(new TripBuilder().InGroup(new GroupId(Guid.CreateVersion7())).Build());

        var found = await _database.Trips.ListByGroupAsync(group, CancellationToken.None);

        found.Select(trip => trip.Id).Should().Equal(mine.Id);
    }

    [TestMethod]
    public async Task ListByGroup_EachTripKeepsItsOwnTravelers()
    {
        var group = new GroupId(Guid.CreateVersion7());
        var travelerA = await SeedTravelerAsync("Traveler A");
        var memberB = await SeedTravelerAsync("Member B");
        var first = await _database.SeedAsync(new TripBuilder().InGroup(group).WithTravelers(travelerA.Id).Build());
        var second = await _database.SeedAsync(new TripBuilder().InGroup(group).WithTravelers(memberB.Id).Build());

        var found = await _database.Trips.ListByGroupAsync(group, CancellationToken.None);

        found.Single(trip => trip.Id == first.Id).Travelers.Should().Equal(travelerA.Id);
        found.Single(trip => trip.Id == second.Id).Travelers.Should().Equal(memberB.Id);
    }

    [TestMethod]
    public async Task ListByGroup_GroupWithoutTrip_ReturnsNothing()
    {
        var found = await _database.Trips.ListByGroupAsync(new GroupId(Guid.CreateVersion7()), CancellationToken.None);

        found.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListByTraveler_TravelerInTripsOfSeveralGroups_ReturnsThemAllInTheOrderTheyWereCreated()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var memberB = await SeedTravelerAsync("Member B");
        var inFirstGroup = await _database.SeedAsync(
            new TripBuilder().InGroup(new GroupId(Guid.CreateVersion7())).WithTravelers(travelerA.Id, memberB.Id).Build());
        await _database.SeedAsync(new TripBuilder().WithTravelers(memberB.Id).Build());
        var inSecondGroup = await _database.SeedAsync(
            new TripBuilder().InGroup(new GroupId(Guid.CreateVersion7())).WithTravelers(travelerA.Id).Build());

        var found = await _database.Trips.ListByTravelerAsync(travelerA.Id, CancellationToken.None);

        found.Select(trip => trip.Id).Should().Equal(inFirstGroup.Id, inSecondGroup.Id);
        found[0].Travelers.Should().Equal(travelerA.Id, memberB.Id);
    }

    [TestMethod]
    public async Task ListByTraveler_TravelerWithoutTrip_ReturnsNothingAndTheTravelerReadsFine()
    {
        var childC = await SeedTravelerAsync("Child C");

        var trips = await _database.Trips.ListByTravelerAsync(childC.Id, CancellationToken.None);
        var traveler = await _database.Travelers.FindAsync(childC.Id, CancellationToken.None);

        trips.Should().BeEmpty();
        traveler.Should().NotBeNull();
    }

    [TestMethod]
    public async Task RemoveTraveler_TravelerInTheTrip_LeavesTheOthersInTheirOrder()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var memberB = await SeedTravelerAsync("Member B");
        var childC = await SeedTravelerAsync("Child C");
        var trip = await _database.SeedAsync(new TripBuilder().Build());
        await _database.Trips.AddTravelerAsync(trip.Id, childC.Id, CancellationToken.None);
        await _database.Trips.AddTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None);
        await _database.Trips.AddTravelerAsync(trip.Id, memberB.Id, CancellationToken.None);

        await _database.Trips.RemoveTravelerAsync(trip.Id, travelerA.Id, CancellationToken.None);

        (await _database.TravelersOfAsync(trip.Id)).Should().Equal(childC.Id, memberB.Id);
        (await _database.Travelers.FindAsync(travelerA.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [TestMethod]
    public async Task RemoveTraveler_TravelerNotInTheTrip_ChangesNothing()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var memberB = await SeedTravelerAsync("Member B");
        var trip = await _database.SeedAsync(new TripBuilder().WithTravelers(travelerA.Id).Build());

        await _database.Trips.RemoveTravelerAsync(trip.Id, memberB.Id, CancellationToken.None);

        (await _database.TravelersOfAsync(trip.Id)).Should().Equal(travelerA.Id);
    }

    [TestMethod]
    public async Task RemoveTraveler_UnknownTrip_DoesNotThrow()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");

        var remove = async () => await _database.Trips.RemoveTravelerAsync(new TripId(Guid.CreateVersion7()), travelerA.Id, CancellationToken.None);

        await remove.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task Rename_ExistingTrip_ChangesTheNameAndNothingElse()
    {
        var group = new GroupId(Guid.CreateVersion7());
        var creator = UserId.New();
        var travelerA = await SeedTravelerAsync("Traveler A");
        var memberB = await SeedTravelerAsync("Member B");
        var trip = await _database.SeedAsync(
            new TripBuilder().InGroup(group).WithCreator(creator).WithName("Trip to Lisbon").WithTravelers(travelerA.Id, memberB.Id).Build());

        await _database.Trips.RenameAsync(trip.Id, "Trip to Porto", CancellationToken.None);
        var found = await _database.Trips.FindAsync(trip.Id, CancellationToken.None);

        found!.Name.Should().Be("Trip to Porto");
        found.GroupId.Should().Be(group);
        found.CreatorId.Should().Be(creator);
        found.Travelers.Should().Equal(travelerA.Id, memberB.Id);
    }

    [TestMethod]
    public async Task Rename_UnknownTrip_DoesNothingAndDoesNotThrow()
    {
        var unknown = new TripId(Guid.CreateVersion7());

        var rename = async () => await _database.Trips.RenameAsync(unknown, "Trip to Porto", CancellationToken.None);

        await rename.Should().NotThrowAsync();
        (await _database.Trips.FindAsync(unknown, CancellationToken.None)).Should().BeNull();
    }

    [TestMethod]
    public async Task TryAdd_TripWithoutTraveler_ReturnsTrueAndReadsBackWithNone()
    {
        var trip = new TripBuilder().Build();

        var added = await _database.Trips.TryAddAsync(trip, CancellationToken.None);
        var found = await _database.Trips.FindAsync(trip.Id, CancellationToken.None);

        added.Should().BeTrue();
        found.Should().NotBeNull();
        found!.Travelers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TryAdd_TripWithItsCreatorAsTraveler_KeepsTheTraveler()
    {
        var creator = UserId.New();
        var profile = await _database.SeedAsync(new TravelerBuilder().WithOwner(creator).AsProfile().Build());
        var trip = new TripBuilder().WithCreator(creator).WithTravelers(profile.Id).Build();

        var added = await _database.Trips.TryAddAsync(trip, CancellationToken.None);

        added.Should().BeTrue();
        (await _database.TravelersOfAsync(trip.Id)).Should().Equal(profile.Id);
    }

    [TestMethod]
    public async Task TryAdd_TripWhoseStartingTravelerDoesNotExist_ThrowsAndKeepsNoTrip()
    {
        var trip = new TripBuilder().WithTravelers(TravelerId.New()).Build();

        var add = async () => await _database.Trips.TryAddAsync(trip, CancellationToken.None);

        await add.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
        (await _database.Trips.FindAsync(trip.Id, CancellationToken.None)).Should().BeNull("the trip and its travelers are one change");
    }

    [TestMethod]
    public async Task TryAdd_TripWithSeveralStartingTravelers_KeepsThemInTheOrderGiven()
    {
        var first = await SeedTravelerAsync("Traveler A");
        var second = await SeedTravelerAsync("Member B");
        var third = await SeedTravelerAsync("Child C");
        var trip = new TripBuilder().WithTravelers(third.Id, first.Id, second.Id).Build();

        await _database.Trips.TryAddAsync(trip, CancellationToken.None);

        (await _database.TravelersOfAsync(trip.Id)).Should().Equal(third.Id, first.Id, second.Id);
    }

    [TestMethod]
    public async Task TryAdd_IdentifierAlreadyUsed_ReturnsFalseWithoutThrowingAndKeepsTheFirst()
    {
        var travelerA = await SeedTravelerAsync("Traveler A");
        var first = await _database.SeedAsync(new TripBuilder().WithName("Trip to Lisbon").WithTravelers(travelerA.Id).Build());
        var second = new TripBuilder().WithId(first.Id).WithName("Trip to Porto").Build();

        var added = await _database.Trips.TryAddAsync(second, CancellationToken.None);

        added.Should().BeFalse();
        var found = await _database.Trips.FindAsync(first.Id, CancellationToken.None);
        found!.Name.Should().Be("Trip to Lisbon");
        found.Travelers.Should().Equal(travelerA.Id);
    }

    private static async Task<Traveler> SeedTravelerAsync(string name) =>
        await _database.SeedAsync(new TravelerBuilder().WithName(name).Build());
}
