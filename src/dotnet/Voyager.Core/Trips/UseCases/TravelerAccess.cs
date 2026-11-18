using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Who owns a traveler: only the user who declared it. For anyone else the traveler is not found, as if it did not
/// exist, so that its identifier tells nothing.
/// </summary>
internal static class TravelerAccess
{
    #region Public Methods

    /// <summary>
    /// Finds a traveler that belongs to the caller.
    /// </summary>
    /// <param name="travelers">Where the travelers are kept.</param>
    /// <param name="caller">The user who asks.</param>
    /// <param name="id">The traveler.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The traveler, or <see cref="TripsErrors.TravelerNotFound"/> when there is none or it is not the caller's.</returns>
    public static async Task<Result<Traveler>> FindOwnedAsync(
        ITravelerRepository travelers,
        UserId caller,
        TravelerId id,
        CancellationToken cancellationToken)
    {
        var traveler = await travelers.FindAsync(id, cancellationToken);
        return traveler is not null && traveler.OwnerId == caller
            ? Result.Success(traveler)
            : Result.Failure<Traveler>(TripsErrors.TravelerNotFound);
    }

    #endregion Public Methods
}
