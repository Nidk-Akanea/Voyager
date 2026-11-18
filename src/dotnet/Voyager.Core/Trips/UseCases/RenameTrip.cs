using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Renames a trip: any member of its group may. If two members rename it at the same time, the last one wins.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class RenameTrip(ITripRepository trips, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Renames the trip.</summary>
    /// <param name="command">Which trip and its new name.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The renamed trip; or why it is refused.</returns>
    public async Task<Result<Trip>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var access = await TripAccess.FindVisibleAsync(trips, groups, command.Caller, command.Id, cancellationToken);
        if (access.IsFailure)
        {
            return access;
        }

        var renamed = access.Value.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return renamed;
        }

        await trips.RenameAsync(command.Id, renamed.Value.Name, cancellationToken);
        return await TripAccess.ReloadAsync(trips, command.Id, cancellationToken);
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which trip to rename.</summary>
    /// <param name="Caller">The member who asks.</param>
    /// <param name="Id">The trip.</param>
    /// <param name="Name">The new name, as typed.</param>
    public sealed record Command(UserId Caller, TripId Id, string? Name);

    #endregion Types
}
