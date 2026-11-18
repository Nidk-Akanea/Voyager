using AwesomeAssertions;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Trips;

[TestClass]
public sealed class TripTravelersTests
{
    #region Public Methods

    [TestMethod]
    public void Travelers_WrittenThroughTheListInterface_IsRefused()
    {
        var first = TravelerId.New();
        var trip = Trip.Restore(TripId.New(), GroupId.New(), UserId.New(), "Été en Bretagne", [first]);

        var write = () => ((IList<TravelerId>)trip.Travelers)[0] = TravelerId.New();

        write.Should().Throw<NotSupportedException>();
        trip.Travelers.Should().Equal(first);
    }

    [TestMethod]
    public void Rename_ThenWritingToTheRenamedTripsList_ChangesNothingOfTheFirst()
    {
        var first = TravelerId.New();
        var trip = Trip.Restore(TripId.New(), GroupId.New(), UserId.New(), "Été en Bretagne", [first]);
        var renamed = trip.Rename("Bretagne 2027").Value;

        var write = () => ((IList<TravelerId>)renamed.Travelers)[0] = TravelerId.New();

        write.Should().Throw<NotSupportedException>();
        trip.Travelers.Should().Equal(first);
    }

    #endregion Public Methods
}
