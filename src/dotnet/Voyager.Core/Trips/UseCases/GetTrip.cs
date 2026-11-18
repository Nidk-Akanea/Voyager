using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Gives a trip with its travelers in full, to a member of its group: they see everything of the travelers of the trip.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class GetTrip(ITripRepository trips, ITravelerRepository travelers, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Gives the trip.</summary>
    /// <param name="command">Which trip.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The trip and its travelers, in the order they were added; or <see cref="TripsErrors.TripNotFound"/>.</returns>
    public async Task<Result<TripView>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var access = await TripAccess.FindVisibleAsync(trips, groups, command.Caller, command.Id, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<TripView>(access.Error);
        }

        var trip = access.Value;
        var known = new Dictionary<TravelerId, Traveler>();
        foreach (var traveler in await travelers.ListByIdsAsync(trip.Travelers, cancellationToken))
        {
            known[traveler.Id] = traveler;
        }

        var inOrder = new List<Traveler>(trip.Travelers.Count);
        foreach (var travelerId in trip.Travelers)
        {
            if (known.TryGetValue(travelerId, out var traveler))
            {
                inOrder.Add(traveler);
            }
        }

        return Result.Success(new TripView(trip, inOrder));
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which trip to give.</summary>
    /// <param name="Caller">The user who asks.</param>
    /// <param name="Id">The trip.</param>
    public sealed record Command(UserId Caller, TripId Id);

    #endregion Types
}
