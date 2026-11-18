using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Deletes a trip for good. Only its creator does; its travelers are not deleted.
/// </summary>
/// <param name="trips">Where the trips are kept.</param>
/// <param name="groups">Who is in which group.</param>
public sealed class DeleteTrip(ITripRepository trips, IGroupMembership groups)
{
    #region Public Methods

    /// <summary>Deletes the trip.</summary>
    /// <param name="command">Which trip.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Nothing; or why it is refused: not found, or not the creator.</returns>
    public async Task<Result<Unit>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var access = await TripAccess.FindVisibleAsync(trips, groups, command.Caller, command.Id, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<Unit>(access.Error);
        }

        if (access.Value.CreatorId != command.Caller)
        {
            return Result.Failure<Unit>(TripsErrors.NotTheCreator);
        }

        await trips.DeleteAsync(command.Id, cancellationToken);
        return Result.Success();
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which trip to delete.</summary>
    /// <param name="Caller">The member who asks.</param>
    /// <param name="Id">The trip.</param>
    public sealed record Command(UserId Caller, TripId Id);

    #endregion Types
}
