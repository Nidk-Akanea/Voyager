using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Lists the trips a traveler is in, for the warning before it is deleted. Only the owner asks, and only the trips of the
/// groups they belong to are named.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class ListTripsOfTraveler(ITripRepository trips, ITravelerRepository travelers, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Lists the trips.</summary>
    /// <param name="command">Which traveler.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The trips, in the order they were created; or why it is refused.</returns>
    public async Task<Result<IReadOnlyList<Trip>>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var owned = await TravelerAccess.FindOwnedAsync(travelers, command.Caller, command.Id, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Trip>>(owned.Error);
        }

        // One question per group, not per trip.
        var memberOf = new Dictionary<GroupId, bool>();
        var visible = new List<Trip>();
        foreach (var trip in await trips.ListByTravelerAsync(command.Id, cancellationToken))
        {
            if (!memberOf.TryGetValue(trip.GroupId, out var isMember))
            {
                isMember = await groups.IsMemberAsync(command.Caller, trip.GroupId, cancellationToken);
                memberOf[trip.GroupId] = isMember;
            }

            if (isMember)
            {
                visible.Add(trip);
            }
        }

        return Result.Success<IReadOnlyList<Trip>>(visible);
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which traveler to look into.</summary>
    /// <param name="Caller">The user who asks.</param>
    /// <param name="Id">The traveler.</param>
    public sealed record Command(UserId Caller, TravelerId Id);

    #endregion Types
}
