using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Takes a traveler out of a trip: any member of the group may, whoever owns the traveler, who stays what they were
/// otherwise. Taking out one that is not there changes nothing.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class RemoveTravelerFromTrip(ITripRepository trips, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Takes the traveler out.</summary>
    /// <param name="command">Which trip and which traveler.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The trip without the traveler; or why it is refused.</returns>
    public async Task<Result<Trip>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var access = await TripAccess.FindVisibleAsync(trips, groups, command.Caller, command.Trip, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        await trips.RemoveTravelerAsync(command.Trip, command.Traveler, cancellationToken);
        return await TripAccess.ReloadAsync(trips, command.Trip, cancellationToken);
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which traveler to take out of which trip.</summary>
    /// <param name="Caller">The member who asks.</param>
    /// <param name="Trip">The trip.</param>
    /// <param name="Traveler">The traveler.</param>
    public sealed record Command(UserId Caller, TripId Trip, TravelerId Traveler);

    #endregion Types
}
