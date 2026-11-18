using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class RenameTripTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_Creator_RenamesTheTripAndKeepsIdentityCreatorAndTravelers()
    {
        var traveler = _world.AddTravelerOf(_world.Me);
        var trip = _world.AddTripOfMyGroup(traveler.Id);

        var result = await Execute(_world.Me, trip.Id, "Brittany 2027");

        var renamed = result.ShouldSucceed();
        renamed.Name.Should().Be("Brittany 2027");
        renamed.Id.Should().Be(trip.Id);
        renamed.CreatorId.Should().Be(_world.Me);
        renamed.Travelers.Should().Equal(traveler.Id);
        _world.Trips.Find(trip.Id)!.Name.Should().Be("Brittany 2027");
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherMemberOfTheGroup_CanRenameAndTheCreatorStays()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberB, trip.Id, "Brittany 2027");

        result.ShouldSucceed().CreatorId.Should().Be(_world.Me);
        _world.Trips.Find(trip.Id)!.Name.Should().Be("Brittany 2027");
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoMembersRenameInTurn_TheLastSaveWins()
    {
        var trip = _world.AddTripOfMyGroup();

        await Execute(_world.Me, trip.Id, "First name");
        await Execute(_world.MemberB, trip.Id, "Second name");

        _world.Trips.Find(trip.Id)!.Name.Should().Be("Second name");
    }

    [TestMethod]
    public async Task ExecuteAsync_NameWithSpaces_IsKeptTrimmed()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, trip.Id, "  Brittany 2027  ");

        result.ShouldSucceed().Name.Should().Be("Brittany 2027");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task ExecuteAsync_MissingOrBlankName_IsRefusedAndTheTripKeepsItsName(string? name)
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, trip.Id, name);

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.REQUIRED));
        _world.Trips.Find(trip.Id)!.Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public async Task ExecuteAsync_NameOf101Characters_IsRefusedAndTheTripKeepsItsName()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.Me, trip.Id, new string('a', 101));

        result.ShouldFailWith(TripsErrors.Invalid(TripsErrors.NAME_FIELD, TripsErrors.TOO_LONG));
        _world.Trips.Find(trip.Id)!.Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public async Task ExecuteAsync_MemberOfAnotherGroup_ReceivesTripNotFoundAndTheNameStays()
    {
        var trip = _world.AddTripOfMyGroup();

        var result = await Execute(_world.MemberC, trip.Id, "Hijacked");

        result.ShouldFailWith(TripsErrors.TripNotFound);
        _world.Trips.Find(trip.Id)!.Name.Should().Be("Summer in Brittany");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTrip_ReceivesTripNotFound()
    {
        var result = await Execute(_world.Me, TripId.New(), "Brittany 2027");

        result.ShouldFailWith(TripsErrors.TripNotFound);
    }

    #endregion Public Methods

    #region Private Methods

    private Task<Result<Trip>> Execute(UserId caller, TripId id, string? name) =>
        new RenameTrip(_world.Trips, _world.Groups).ExecuteAsync(new RenameTrip.Command(caller, id, name), CancellationToken.None);

    #endregion Private Methods
}
