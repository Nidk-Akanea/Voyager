using AwesomeAssertions;

using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class CreateMyProfileTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_FirstProfile_CreatesATravelerMarkedAsMyProfile()
    {
        var id = TravelerId.New();

        var result = await Execute(_world.Me, id, TestDetails.Of("Traveler A", "TA"));

        var profile = result.ShouldSucceed();
        profile.Id.Should().Be(id);
        profile.OwnerId.Should().Be(_world.Me);
        profile.IsProfile.Should().BeTrue();
        profile.DisplayName.Should().Be("TA");
        _world.Travelers.All.Should().ContainSingle().Which.Id.Should().Be(id);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnlyAName_Succeeds()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));

        var profile = result.ShouldSucceed();
        profile.BirthDate.Should().BeNull();
        profile.Avatar.Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_ProfileCreated_AppearsAmongMyTravelersAsMyProfile()
    {
        await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));

        var mine = await new ListMyTravelers(_world.Travelers).ExecuteAsync(new ListMyTravelers.Command(_world.Me), CancellationToken.None);

        mine.Should().ContainSingle().Which.IsProfile.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_BlankName_IsRefusedAndNothingIsKept()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("   "));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
        _world.Travelers.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarIsATextFile_IsRefusedAndNothingIsKept()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(), AvatarChange.Replace(TestImages.Text()));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
        _world.Travelers.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_PngAvatar_IsKeptWithTheProfile()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(), AvatarChange.Replace(TestImages.Png()));

        result.ShouldSucceed().Avatar!.Format.Should().Be(AvatarFormat.Png);
    }

    [TestMethod]
    public async Task ExecuteAsync_BirthDateTheDayAfterTomorrow_IsRefusedUsingTheClockOfTheServer()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(birthDate: TripsWorld.Today.PlusDays(2)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.BIRTH_DATE_FIELD, TripsErrors.IN_THE_FUTURE));
    }

    [TestMethod]
    public async Task ExecuteAsync_BirthDateTomorrow_IsAccepted()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(birthDate: TripsWorld.Today.PlusDays(1)));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_ClockLateInTheDayInUtc_JudgesTheBirthDateByTheUtcDate()
    {
        _world.Clock.Reset(Instant.FromUtc(2026, 11, 18, 23, 59));

        var accepted = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(birthDate: new LocalDate(2026, 11, 19)));
        var refused = await Execute(_world.MemberB, TravelerId.New(), TestDetails.Of(birthDate: new LocalDate(2026, 11, 20)));

        accepted.IsSuccess.Should().BeTrue();
        refused.IsFailure.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_SameCreationSentTwice_SucceedsWithoutDuplicate()
    {
        var id = TravelerId.New();
        var first = await Execute(_world.Me, id, TestDetails.Of("Traveler A", "TA"));

        var second = await Execute(_world.Me, id, TestDetails.Of("Traveler A", "TA"));

        second.ShouldSucceed().Id.Should().Be(first.ShouldSucceed().Id);
        _world.Travelers.All.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ExecuteAsync_SameCreationWithAvatarSentTwice_SucceedsWithoutDuplicate()
    {
        var id = TravelerId.New();
        await Execute(_world.Me, id, TestDetails.Of(), AvatarChange.Replace(TestImages.Png()));

        var second = await Execute(_world.Me, id, TestDetails.Of(), AvatarChange.Replace(TestImages.Png()));

        second.IsSuccess.Should().BeTrue();
        _world.Travelers.All.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierWithOtherContent_IsRefusedAsAConflictAndTheProfileIsUnchanged()
    {
        var id = TravelerId.New();
        await Execute(_world.Me, id, TestDetails.Of("Traveler A"));

        var second = await Execute(_world.Me, id, TestDetails.Of("Traveler B"));

        second.IsFailure.Should().BeTrue();
        second.Error.Kind.Should().Be(ErrorKind.Conflict);
        _world.Travelers.Find(id)!.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherProfileWhileOneExists_IsRefusedAsProfileAlreadyExists()
    {
        await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));

        var second = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));

        second.ShouldFailWith(TripsErrors.ProfileAlreadyExists);
        _world.Travelers.All.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherProfileWhileOneExistsAndDeclaredTravelersToo_StillRefused()
    {
        await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));
        await new DeclareTraveler(_world.Travelers, _world.Clock).ExecuteAsync(
            new DeclareTraveler.Command(_world.Me, TravelerId.New(), TestDetails.Of("Child C"), AvatarChange.Keep),
            CancellationToken.None);

        var second = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler B"));

        second.ShouldFailWith(TripsErrors.ProfileAlreadyExists);
    }

    [TestMethod]
    public async Task ExecuteAsync_IdentifierUsedByATravelerOfSomeoneElse_IsRefusedAsIdAlreadyUsed()
    {
        var taken = _world.AddTravelerOf(_world.MemberB, "Traveler B");

        var result = await Execute(_world.Me, taken.Id, TestDetails.Of("Traveler B"));

        result.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Travelers.All.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ExecuteAsync_IdentifierUsedByOneOfMyOrdinaryTravelers_IsRefusedAsIdAlreadyUsed()
    {
        var declared = _world.AddTravelerOf(_world.Me, "Child C");

        var result = await Execute(_world.Me, declared.Id, TestDetails.Of("Child C"));

        result.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Travelers.Find(declared.Id)!.IsProfile.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoUsers_EachGetTheirOwnProfile()
    {
        var mine = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));
        var theirs = await Execute(_world.MemberB, TravelerId.New(), TestDetails.Of("Traveler B"));

        mine.IsSuccess.Should().BeTrue();
        theirs.IsSuccess.Should().BeTrue();
        _world.Travelers.All.Should().HaveCount(2);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Traveler>> Execute(
        UserId caller,
        TravelerId id,
        TravelerDetails details,
        AvatarChange avatar = default) =>
        new CreateMyProfile(_world.Travelers, _world.Clock)
            .ExecuteAsync(new CreateMyProfile.Command(caller, id, details, avatar), CancellationToken.None);

    #endregion Private Methods
}
