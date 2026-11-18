using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Adds one of the caller's travelers to a trip of their group, after the others. A member adds their own travelers
/// only; adding one that is already there changes nothing.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class AddTravelerToTrip(ITripRepository trips, ITravelerRepository travelers, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Adds the traveler.</summary>
    /// <param name="command">Which trip and which traveler.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The trip with the traveler; or why it is refused.</returns>
    public async Task<Result<Trip>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var access = await TripAccess.FindVisibleAsync(trips, groups, command.Caller, command.Trip, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        var owned = await TravelerAccess.FindOwnedAsync(travelers, command.Caller, command.Traveler, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<Trip>(owned.Error);
        }

        await trips.AddTravelerAsync(command.Trip, command.Traveler, cancellationToken);
        return await TripAccess.ReloadAsync(trips, command.Trip, cancellationToken);
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which traveler to add to which trip.</summary>
    /// <param name="Caller">The member who asks, who owns the traveler.</param>
    /// <param name="Trip">The trip.</param>
    /// <param name="Traveler">The traveler.</param>
    public sealed record Command(UserId Caller, TripId Trip, TravelerId Traveler);

    #endregion Types
}
