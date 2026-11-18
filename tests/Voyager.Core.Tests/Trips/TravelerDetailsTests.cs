using AwesomeAssertions;

using NodaTime;

using Voyager.Core.Shared;
using Voyager.Core.Trips;
using Voyager.Core.Trips.UseCases;

namespace Voyager.Core.Tests.Trips;

[TestClass]
public sealed class TravelerDetailsTests
{
    #region Fields

    private static readonly TravelerDetails _details =
        new("Traveler A", "TA", new LocalDate(1990, 3, 15), "Likes night trains");

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public void ToString_WithPersonalData_WritesNoneOfIt()
    {
        var text = _details.ToString();

        text.Should().NotContain("Traveler A").And.NotContain("1990").And.NotContain("night trains");
    }

    [TestMethod]
    public void ToString_OfTheCommandsThatHoldIt_WritesNoneOfIt()
    {
        var texts = new[]
        {
            new DeclareTraveler.Command(UserId.New(), TravelerId.New(), _details, AvatarChange.Keep).ToString(),
            new CreateMyProfile.Command(UserId.New(), TravelerId.New(), _details, AvatarChange.Keep).ToString(),
            new UpdateTraveler.Command(UserId.New(), TravelerId.New(), _details, AvatarChange.Keep).ToString(),
        };

        texts.Should().AllSatisfy(text => text.Should().NotContain("Traveler A").And.NotContain("1990").And.NotContain("night trains"));
    }

    #endregion Public Methods
}
