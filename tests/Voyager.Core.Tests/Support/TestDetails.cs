using NodaTime;

using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// What a user types when they declare a traveler, for the tests of creation and update.
/// </summary>
internal static class TestDetails
{
    #region Public Methods

    public static TravelerDetails Of(
        string? name = "Traveler A",
        string? nickname = null,
        LocalDate? birthDate = null,
        string? description = null) =>
        new(name, nickname, birthDate, description);

    #endregion Public Methods
}
