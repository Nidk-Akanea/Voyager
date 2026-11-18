using AwesomeAssertions;

using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Trips;
using Voyager.Persistence.Tests.Support;

namespace Voyager.Persistence.Tests.Trips;

[TestClass]
public sealed class TravelerRepositoryTests
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
    public async Task Delete_TravelerInTrips_TakesItOutOfEveryTripAndLeavesTheOthers()
    {
        var owner = UserId.New();
        var travelerA = await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).AsProfile().Build());
        var childC = await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithName("Child C").Build());
        var firstTrip = await _database.SeedAsync(new TripBuilder().WithTravelers(travelerA.Id, childC.Id).Build());
        var secondTrip = await _database.SeedAsync(new TripBuilder().WithTravelers(childC.Id, travelerA.Id).Build());

        await _database.Travelers.DeleteAsync(travelerA.Id, CancellationToken.None);

        (await _database.Travelers.FindAsync(travelerA.Id, CancellationToken.None)).Should().BeNull();
        (await _database.Travelers.FindAsync(childC.Id, CancellationToken.None)).Should().NotBeNull();
        (await _database.TravelersOfAsync(firstTrip.Id)).Should().Equal(childC.Id);
        (await _database.TravelersOfAsync(secondTrip.Id)).Should().Equal(childC.Id);
    }

    [TestMethod]
    public async Task Delete_UnknownTraveler_DoesNotThrow()
    {
        var unknown = TravelerId.New();

        var delete = async () => await _database.Travelers.DeleteAsync(unknown, CancellationToken.None);

        await delete.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task Find_UnknownIdentifier_ReturnsNull()
    {
        var found = await _database.Travelers.FindAsync(TravelerId.New(), CancellationToken.None);

        found.Should().BeNull();
    }

    [TestMethod]
    public async Task FindProfile_OwnerWithoutProfile_ReturnsNull()
    {
        var owner = UserId.New();
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithName("Child C").Build());

        var found = await _database.Travelers.FindProfileAsync(owner, CancellationToken.None);

        found.Should().BeNull();
    }

    [TestMethod]
    public async Task FindProfile_OwnerWithProfileAndOtherTravelers_ReturnsTheProfile()
    {
        var owner = UserId.New();
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithName("Child C").Build());
        var profile = await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).AsProfile().Build());

        var found = await _database.Travelers.FindProfileAsync(owner, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(profile.Id);
        found.IsProfile.Should().BeTrue();
    }

    [TestMethod]
    public async Task ListByIds_KnownAndUnknownIdentifiers_ReturnsTheExistingOnesWhoeverOwnsThem()
    {
        var travelerA = await _database.SeedAsync(new TravelerBuilder().Build());
        var memberB = await _database.SeedAsync(new TravelerBuilder().WithName("Member B").Build());
        await _database.SeedAsync(new TravelerBuilder().WithName("Child C").Build());

        var found = await _database.Travelers.ListByIdsAsync([travelerA.Id, memberB.Id, TravelerId.New()], CancellationToken.None);

        found.Select(traveler => traveler.Id).Should().BeEquivalentTo([travelerA.Id, memberB.Id]);
    }

    [TestMethod]
    public async Task ListByIds_NoIdentifier_ReturnsNothing()
    {
        var found = await _database.Travelers.ListByIdsAsync([], CancellationToken.None);

        found.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListByOwner_SeveralTravelers_ReturnsThemInTheOrderTheyWereDeclared()
    {
        var owner = UserId.New();

        // Identifiers made in one order, travelers declared in another: the order of the declarations wins.
        var first = TravelerId.New();
        var second = TravelerId.New();
        var third = TravelerId.New();
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithId(third).WithName("Traveler A").AsProfile().Build());
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithId(first).WithName("Child C").Build());
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithId(second).WithName("Member B").Build());

        var found = await _database.Travelers.ListByOwnerAsync(owner, CancellationToken.None);

        found.Select(traveler => traveler.Id).Should().Equal(third, first, second);
    }

    [TestMethod]
    public async Task ListByOwner_TravelersOfAnotherUser_LeavesThemOut()
    {
        var owner = UserId.New();
        var mine = await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).Build());
        await _database.SeedAsync(new TravelerBuilder().WithOwner(UserId.New()).WithName("Member B").Build());

        var found = await _database.Travelers.ListByOwnerAsync(owner, CancellationToken.None);

        found.Select(traveler => traveler.Id).Should().Equal(mine.Id);
    }

    [TestMethod]
    public async Task ListByOwner_UserWithoutTraveler_ReturnsNothing()
    {
        var found = await _database.Travelers.ListByOwnerAsync(UserId.New(), CancellationToken.None);

        found.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TryAdd_ThenFind_ReturnsEveryFieldUnchanged()
    {
        var avatar = TestImages.PngWithPattern(Avatar.MAXIMUM_BYTES);
        var traveler = new TravelerBuilder()
            .WithName("Child C")
            .WithNickname("Cee")
            .WithBirthDate(new LocalDate(2016, 2, 29))
            .WithDescription("Likes trains and ice cream.")
            .WithAvatar(avatar, AvatarFormat.Png)
            .Build();

        await _database.SeedAsync(traveler);
        var found = await _database.Travelers.FindAsync(traveler.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(traveler.Id);
        found.OwnerId.Should().Be(traveler.OwnerId);
        found.IsProfile.Should().BeFalse();
        found.Name.Should().Be("Child C");
        found.Nickname.Should().Be("Cee");
        found.BirthDate.Should().Be(new LocalDate(2016, 2, 29));
        found.Description.Should().Be("Likes trains and ice cream.");
        found.Avatar.Should().NotBeNull();
        found.Avatar!.Format.Should().Be(AvatarFormat.Png);
        found.Avatar.Content.ToArray().SequenceEqual(avatar).Should().BeTrue("the avatar comes back byte for byte");
    }

    [TestMethod]
    public async Task TryAdd_TravelerWithNothingOptional_ReadsBackWithoutIt()
    {
        var traveler = new TravelerBuilder().AsProfile().Build();

        await _database.SeedAsync(traveler);
        var found = await _database.Travelers.FindAsync(traveler.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.IsProfile.Should().BeTrue();
        found.Nickname.Should().BeNull();
        found.BirthDate.Should().BeNull();
        found.Description.Should().BeNull();
        found.Avatar.Should().BeNull();
    }

    [TestMethod]
    public async Task TryAdd_NewIdentifier_ReturnsTrue()
    {
        var added = await _database.Travelers.TryAddAsync(new TravelerBuilder().Build(), CancellationToken.None);

        added.Should().BeTrue();
    }

    [TestMethod]
    public async Task TryAdd_IdentifierAlreadyUsed_ReturnsFalseAndKeepsTheFirst()
    {
        var first = await _database.SeedAsync(new TravelerBuilder().WithName("Traveler A").Build());
        var second = new TravelerBuilder().WithId(first.Id).WithName("Member B").Build();

        var added = await _database.Travelers.TryAddAsync(second, CancellationToken.None);

        added.Should().BeFalse();
        (await _database.Travelers.FindAsync(first.Id, CancellationToken.None))!.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public async Task TryAdd_SecondProfileOfTheSameOwner_ReturnsFalseWithoutThrowing()
    {
        var owner = UserId.New();
        var profile = await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).AsProfile().Build());
        var second = new TravelerBuilder().WithOwner(owner).WithName("Member B").AsProfile().Build();

        var added = await _database.Travelers.TryAddAsync(second, CancellationToken.None);

        added.Should().BeFalse();
        (await _database.Travelers.FindAsync(second.Id, CancellationToken.None)).Should().BeNull();
        (await _database.Travelers.FindProfileAsync(owner, CancellationToken.None))!.Id.Should().Be(profile.Id);
    }

    [TestMethod]
    public async Task TryAdd_ProfileOfAnotherOwner_ReturnsTrue()
    {
        await _database.SeedAsync(new TravelerBuilder().AsProfile().Build());

        var added = await _database.Travelers.TryAddAsync(new TravelerBuilder().WithName("Member B").AsProfile().Build(), CancellationToken.None);

        added.Should().BeTrue();
    }

    [TestMethod]
    public async Task TryAdd_SeveralTravelersThatAreNotProfilesOfTheSameOwner_ReturnsTrueForEach()
    {
        var owner = UserId.New();
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).AsProfile().Build());
        await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).WithName("Child C").Build());

        var added = await _database.Travelers.TryAddAsync(new TravelerBuilder().WithOwner(owner).WithName("Member B").Build(), CancellationToken.None);

        added.Should().BeTrue();
        (await _database.Travelers.ListByOwnerAsync(owner, CancellationToken.None)).Should().HaveCount(3);
    }

    [TestMethod]
    public async Task Update_ChangedContent_KeepsTheNewContentButNotTheOwnerNorTheProfileMark()
    {
        var owner = UserId.New();
        var original = await _database.SeedAsync(new TravelerBuilder().WithOwner(owner).AsProfile().WithName("Traveler A").Build());
        var changed = new TravelerBuilder()
            .WithId(original.Id)
            .WithOwner(UserId.New())
            .WithName("Member B")
            .WithNickname("Bee")
            .WithBirthDate(new LocalDate(1990, 7, 14))
            .WithDescription("Plans the trips.")
            .WithAvatar(TestImages.Jpeg(300), AvatarFormat.Jpeg)
            .Build();

        await _database.Travelers.UpdateAsync(changed, CancellationToken.None);
        var found = await _database.Travelers.FindAsync(original.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Name.Should().Be("Member B");
        found.Nickname.Should().Be("Bee");
        found.BirthDate.Should().Be(new LocalDate(1990, 7, 14));
        found.Description.Should().Be("Plans the trips.");
        found.Avatar!.Format.Should().Be(AvatarFormat.Jpeg);
        found.Avatar.Content.ToArray().Should().Equal(TestImages.Jpeg(300));
        found.OwnerId.Should().Be(owner);
        found.IsProfile.Should().BeTrue();
    }

    [TestMethod]
    public async Task Update_WithoutAvatar_RemovesTheAvatar()
    {
        var original = await _database.SeedAsync(new TravelerBuilder().WithAvatar(TestImages.Png(200), AvatarFormat.Png).Build());
        var withoutAvatar = new TravelerBuilder().WithId(original.Id).WithOwner(original.OwnerId).Build();

        await _database.Travelers.UpdateAsync(withoutAvatar, CancellationToken.None);
        var found = await _database.Travelers.FindAsync(original.Id, CancellationToken.None);
        var emptyColumns = await _database.ColumnAsync(
            $"SELECT (avatar_content IS NULL AND avatar_format IS NULL)::text FROM travelers WHERE id = '{original.Id.Value}'");

        found!.Avatar.Should().BeNull();
        emptyColumns.Should().Equal("true");
    }

    [TestMethod]
    public async Task Update_WithoutOptionalTexts_ClearsThem()
    {
        var original = await _database.SeedAsync(
            new TravelerBuilder().WithNickname("Ay").WithBirthDate(new LocalDate(1985, 1, 2)).WithDescription("Notes.").Build());
        var cleared = new TravelerBuilder().WithId(original.Id).WithOwner(original.OwnerId).Build();

        await _database.Travelers.UpdateAsync(cleared, CancellationToken.None);
        var found = await _database.Travelers.FindAsync(original.Id, CancellationToken.None);

        found!.Nickname.Should().BeNull();
        found.BirthDate.Should().BeNull();
        found.Description.Should().BeNull();
    }

    [TestMethod]
    public async Task Update_UnknownTraveler_KeepsNothing()
    {
        var unknown = new TravelerBuilder().Build();

        var update = async () => await _database.Travelers.UpdateAsync(unknown, CancellationToken.None);

        await update.Should().NotThrowAsync();
        (await _database.Travelers.FindAsync(unknown.Id, CancellationToken.None)).Should().BeNull();
    }
}
