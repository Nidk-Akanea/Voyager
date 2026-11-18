using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Who sees a trip: the members of its group, and no one else. A trip of another group is not found, as if it did not
/// exist, so that its identifier tells nothing.
/// </summary>
internal static class TripAccess
{
    #region Public Methods

    /// <summary>
    /// Finds a trip the caller may see.
    /// </summary>
    /// <param name="trips">Where the trips are kept.</param>
    /// <param name="groups">Who is in which group.</param>
    /// <param name="caller">The user who asks.</param>
    /// <param name="id">The trip.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The trip, or <see cref="TripsErrors.TripNotFound"/> when there is none or it is not the caller's to see.</returns>
    public static async Task<Result<Trip>> FindVisibleAsync(
        ITripRepository trips,
        IGroupMembership groups,
        UserId caller,
        TripId id,
        CancellationToken cancellationToken)
    {
        var trip = await trips.FindAsync(id, cancellationToken);
        return trip is not null && await groups.IsMemberAsync(caller, trip.GroupId, cancellationToken)
            ? Result.Success(trip)
            : Result.Failure<Trip>(TripsErrors.TripNotFound);
    }

    /// <summary>
    /// Reads a trip again once it was changed, as it is now: other members may have changed it meanwhile.
    /// </summary>
    /// <param name="trips">Where the trips are kept.</param>
    /// <param name="id">The trip.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The trip, or <see cref="TripsErrors.TripNotFound"/> when it was deleted in the meantime.</returns>
    public static async Task<Result<Trip>> ReloadAsync(ITripRepository trips, TripId id, CancellationToken cancellationToken) =>
        await trips.FindAsync(id, cancellationToken) is { } trip
            ? Result.Success(trip)
            : Result.Failure<Trip>(TripsErrors.TripNotFound);

    #endregion Public Methods
}
