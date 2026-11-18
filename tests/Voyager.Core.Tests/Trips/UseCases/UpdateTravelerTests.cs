using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class UpdateTravelerTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_OwnerChangesTheDetails_KeepsThemUnderTheSameIdentifier()
    {
        var traveler = _world.AddTravelerOf(_world.Me);

        var result = await Execute(_world.Me, traveler.Id, TestDetails.Of("Traveler A", "TA"));

        result.ShouldSucceed().Nickname.Should().Be("TA");
        _world.Travelers.Find(traveler.Id)!.Nickname.Should().Be("TA");
        _world.Travelers.All.Should().ContainSingle();
    }

    [TestMethod]
    public async Task ExecuteAsync_OwnerUpdatesTheirProfile_ItStaysTheProfile()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).AsProfile());

        var result = await Execute(_world.Me, profile.Id, TestDetails.Of("Traveler A, renamed"));

        result.ShouldSucceed().IsProfile.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherUser_ReceivesTravelerNotFoundAndNothingChanges()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");

        var result = await Execute(_world.MemberB, traveler.Id, TestDetails.Of("Hijacked"));

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
        _world.Travelers.Find(traveler.Id)!.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfTheSameGroupSeeingTheTravelerInATrip_StillCannotChangeIt()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");
        _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.MemberB, traveler.Id, TestDetails.Of("Hijacked"));

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTraveler_ReceivesTravelerNotFound()
    {
        var result = await Execute(_world.Me, TravelerId.New(), TestDetails.Of());

        result.ShouldFailWith(TripsErrors.TravelerNotFound);
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidDetails_AreRefusedAndTheTravelerIsUnchanged()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");

        var result = await Execute(_world.Me, traveler.Id, TestDetails.Of(new string('a', 101)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
        _world.Travelers.Find(traveler.Id)!.Name.Should().Be("Traveler A");
    }

    [TestMethod]
    public async Task ExecuteAsync_BirthDateTheDayAfterTomorrow_IsRefused()
    {
        var traveler = _world.AddTravelerOf(_world.Me);

        var result = await Execute(_world.Me, traveler.Id, TestDetails.Of(birthDate: TripsWorld.Today.PlusDays(2)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.BIRTH_DATE_FIELD, TripsErrors.IN_THE_FUTURE));
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarKeep_KeepsTheCurrentAvatar()
    {
        var traveler = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithAvatar(TestImages.Png()));

        var result = await Execute(_world.Me, traveler.Id, TestDetails.Of(), AvatarChange.Keep);

        result.ShouldSucceed().Avatar!.HasSameContentAs(traveler.Avatar!).Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarRemove_RemovesTheAvatar()
    {
        var traveler = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithAvatar(TestImages.Png()));

        await Execute(_world.Me, traveler.Id, TestDetails.Of(), AvatarChange.Remove);

        _world.Travelers.Find(traveler.Id)!.Avatar.Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarReplace_ReplacesTheAvatar()
    {
        var traveler = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithAvatar(TestImages.Png()));

        await Execute(_world.Me, traveler.Id, TestDetails.Of(), AvatarChange.Replace(TestImages.WebP()));

        _world.Travelers.Find(traveler.Id)!.Avatar!.Format.Should().Be(AvatarFormat.WebP);
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarReplacedByATextFile_IsRefusedAndTheAvatarDoesNotChange()
    {
        var traveler = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithAvatar(TestImages.Png()));

        var result = await Execute(_world.Me, traveler.Id, TestDetails.Of(), AvatarChange.Replace(TestImages.Text()));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.NOT_AN_IMAGE));
        _world.Travelers.Find(traveler.Id)!.Avatar!.Format.Should().Be(AvatarFormat.Png);
    }

    [TestMethod]
    public async Task ExecuteAsync_AvatarReplacedByAnImageOfThreeMegabytes_IsRefusedAndTheAvatarDoesNotChange()
    {
        var traveler = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithAvatar(TestImages.Png()));

        var result = await Execute(
            _world.Me,
            traveler.Id,
            TestDetails.Of(),
            AvatarChange.Replace(TestImages.Png(3 * 1024 * 1024)));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.AVATAR_FIELD, TripsErrors.TOO_LARGE));
        _world.Travelers.Find(traveler.Id)!.Avatar!.Content.Length.Should().Be(64);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerInTwoTrips_ShowsTheNewNicknameInBoth()
    {
        var traveler = _world.AddTravelerOf(_world.Me, "Traveler A");
        var first = _world.AddTripOfMyGroup(traveler.Id);
        var second = _world.AddTripOfMyGroup(traveler.Id);
        await Execute(_world.Me, traveler.Id, TestDetails.Of("Traveler A", "TA"));
        var getTrip = new GetTrip(_world.Trips, _world.Travelers, _world.Groups);

        var inFirst = await getTrip.ExecuteAsync(new GetTrip.Command(_world.Me, first.Id), CancellationToken.None);
        var inSecond = await getTrip.ExecuteAsync(new GetTrip.Command(_world.MemberB, second.Id), CancellationToken.None);

        inFirst.ShouldSucceed().Travelers.Single().DisplayName.Should().Be("TA");
        inSecond.ShouldSucceed().Travelers.Single().DisplayName.Should().Be("TA");
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Traveler>> Execute(
        UserId caller,
        TravelerId id,
        TravelerDetails details,
        AvatarChange avatar = default) =>
        new UpdateTraveler(_world.Travelers, _world.Clock)
            .ExecuteAsync(new UpdateTraveler.Command(caller, id, details, avatar), CancellationToken.None);

    #endregion Private Methods
}
