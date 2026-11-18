using AwesomeAssertions;

using Voyager.Core.Tests.Support;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips.UseCases;

[TestClass]
public sealed class ListMyTravelersTests
{
    #region Fields

    private readonly TripsWorld _world = new();

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task ExecuteAsync_ProfileAndDeclaredTravelers_AreAllListedInTheOrderDeclared()
    {
        var profile = _world.AddTraveler(new TravelerBuilder().WithOwner(_world.Me).WithName("Member A").AsProfile());
        var first = _world.AddTravelerOf(_world.Me, "Traveler A");
        var second = _world.AddTravelerOf(_world.Me, "Child C");

        var mine = await Execute();

        mine.Select(traveler => traveler.Id).Should().Equal(profile.Id, first.Id, second.Id);
        mine[0].IsProfile.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelersOfOtherUsers_AreNotListed()
    {
        var mine = _world.AddTravelerOf(_world.Me, "Traveler A");
        _world.AddTravelerOf(_world.MemberB, "Traveler B");

        var result = await Execute();

        result.Select(traveler => traveler.Id).Should().Equal(mine.Id);
    }

    [TestMethod]
    public async Task ExecuteAsync_TravelerOfAnotherUserInMyTrip_IsNotListed()
    {
        var theirs = _world.AddTravelerOf(_world.MemberB, "Traveler B");
        _world.AddTripOfMyGroup(theirs.Id);

        var result = await Execute();

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_NoTravelerYet_IsEmpty()
    {
        var result = await Execute();

        result.Should().BeEmpty();
    }

    #endregion Public Methods

    #region Private Methods

    private Task<IReadOnlyList<Traveler>> Execute() =>
        new ListMyTravelers(_world.Travelers).ExecuteAsync(new ListMyTravelers.Command(_world.Me), CancellationToken.None);

    #endregion Private Methods
}
