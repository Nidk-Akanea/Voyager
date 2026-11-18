using NodaTime;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// The day the server is in, as the rules about dates of calendar need it.
/// </summary>
internal static class Days
{
    #region Public Methods

    /// <summary>Today, in UTC.</summary>
    /// <param name="clock">The clock of the server.</param>
    /// <returns>The calendar date.</returns>
    public static LocalDate Today(IClock clock) => clock.GetCurrentInstant().InUtc().Date;

    #endregion Public Methods
}
