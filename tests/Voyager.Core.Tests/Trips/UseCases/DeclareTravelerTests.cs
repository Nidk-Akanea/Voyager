using AwesomeAssertions;

using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class DeclareTravelerTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_NameNicknameAndBirthDate_KeepsAnOrdinaryTravelerOfTheCaller()
    {
        var id = TravelerId.New();

        var result = await Execute(_world.Me, id, TestDetails.Of("Traveler A", "TA", new LocalDate(1990, 3, 15)));

        var traveler = result.ShouldSucceed();
        traveler.OwnerId.Should().Be(_world.Me);
        traveler.IsProfile.Should().BeFalse();
        traveler.DisplayName.Should().Be("TA");
        traveler.BirthDate.Should().Be(new LocalDate(1990, 3, 15));
        _world.Travelers.Find(id).Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_ChildWithoutBirthDate_Succeeds()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Child C"));

        var traveler = result.ShouldSucceed();
        traveler.BirthDate.Should().BeNull();
        traveler.DisplayName.Should().Be("Child C");
    }

    [TestMethod]
    public async Task ExecuteAsync_ProfileAndTwoTravelers_AreListedInTheOrderTheyWereDeclared()
    {
        _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithName("Me").AsProfile());
        await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A", "TA"));
        await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Child C"));

        var mine = await new ListMyTravelers(_world.Travelers).ExecuteAsync(new ListMyTravelers.Command(_world.Me), CancellationToken.None);

        mine.Select(traveler => traveler.Name).Should().Equal("Me", "Traveler A", "Child C");
    }

    [TestMethod]
    public async Task ExecuteAsync_CallerWithoutProfile_StillDeclaresTravelers()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Child C"));

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_CallerWithAProfile_DoesNotCreateASecondProfile()
    {
        _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());

        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Child C"));

        result.ShouldSucceed().IsProfile.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTravelersWithTheSameName_AreBothKept()
    {
        await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));

        var second = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("Traveler A"));

        second.IsSuccess.Should().BeTrue();
        _world.Travelers.All.Should().HaveCount(2);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task ExecuteAsync_EmptyName_IsRefusedAndNothingIsKept(string name)
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(name));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
        _world.Travelers.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_NameOf101Characters_IsRefusedAsTooLong()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(new string('a', 101)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
        _world.Travelers.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_NicknameOf51Characters_IsRefusedAsTooLong()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(nickname: new string('t', 51)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NICKNAME_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public async Task ExecuteAsync_DescriptionOf501Characters_IsRefusedAsTooLong()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(description: new string('d', 501)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.DESCRIPTION_FIELD, TripsErrors.TOO_LONG));
    }

    [TestMethod]
    public async Task ExecuteAsync_NameWithBidirectionalOverride_IsKeptCleaned()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of("‮Traveler A"));

        result.ShouldSucceed().Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public async Task ExecuteAsync_PngAvatarOfOneMegabyte_IsKeptForTheTraveler()
    {
        var result = await Execute(
            _world.Me,
            TravelerId.New(),
            TestDetails.Of(),
            AvatarChange.Replace(TestImages.Png(1024 * 1024)));

        result.ShouldSucceed().Avatar!.Content.Length.Should().Be(1024 * 1024);
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarThatIsATextFile_IsRefusedAndNothingIsKept()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of(), AvatarChange.Replace(TestImages.Text()));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
        _world.Travelers.All.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarOfThreeMegabytes_IsRefusedAsTooLarge()
    {
        var result = await Execute(
            _world.Me,
            TravelerId.New(),
            TestDetails.Of(),
            AvatarChange.Replace(TestImages.Png(3 * 1024 * 1024)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
    }

    [TestMethod]
    public async Task ExecuteAsync_BirthDateTheDayAfterTomorrow_IsRefused()
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
    public async Task ExecuteAsync_SameCreationSentTwice_SucceedsWithoutDuplicate()
    {
        var id = TravelerId.New();
        var first = await Execute(_world.Me, id, TestDetails.Of("Traveler A", "TA"), AvatarChange.Replace(TestImages.Png()));

        var second = await Execute(_world.Me, id, TestDetails.Of("Traveler A", "TA"), AvatarChange.Replace(TestImages.Png()));

        second.ShouldSucceed().Id.Should().Be(first.ShouldSucceed().Id);
        _world.Travelers.All.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierWithOtherContent_IsRefusedAsIdAlreadyUsedAndNothingChanges()
    {
        var id = TravelerId.New();
        await Execute(_world.Me, id, TestDetails.Of("Traveler A"));

        var second = await Execute(_world.Me, id, TestDetails.Of("Traveler B"));

        second.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Travelers.All.Should().ContainSingle().Which.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierWithOtherAvatar_IsRefusedAsIdAlreadyUsed()
    {
        var id = TravelerId.New();
        await Execute(_world.Me, id, TestDetails.Of(), AvatarChange.Replace(TestImages.Png(64)));

        var second = await Execute(_world.Me, id, TestDetails.Of(), AvatarChange.Replace(TestImages.Png(65)));

        second.ShouldFailWith(TripsErrors.IdAlreadyUsed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentifierAndContentFromAnotherUser_IsRefusedAsIdAlreadyUsed()
    {
        var id = TravelerId.New();
        await Execute(_world.Me, id, TestDetails.Of("Traveler A"));

        var second = await Execute(_world.MemberB, id, TestDetails.Of("Traveler A"));

        second.ShouldFailWith(TripsErrors.IdAlreadyUsed);
        _world.Travelers.Find(id)!.OwnerId.Should().Be(_world.Me);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Traveler>> Execute(
        UserId caller,
        TravelerId id,
        TravelerDetails details,
        AvatarChange avatar = default) =>
        new DeclareTraveler(_world.Travelers, _world.Clock)
            .ExecuteAsync(new DeclareTraveler.Command(caller, id, details, avatar), CancellationToken.None);

    #endregion Private Methods
}
