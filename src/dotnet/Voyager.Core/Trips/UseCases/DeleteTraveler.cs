using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Deletes a traveler for good, and takes it out of every trip it is in. Only its owner does, and never the profile.
/// </summary>
/// <param name="travelers">Where the travelers are kept.</param>
public sealed class DeleteTraveler(ITravelerRepository travelers)
{
    #region Public Methods

    /// <summary>Deletes the traveler.</summary>
    /// <param name="command">Which traveler.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>Nothing, or why it is refused.</returns>
    public async Task<Result<Unit>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var owned = await TravelerAccess.FindOwnedAsync(travelers, command.Caller, command.Id, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<Unit>(owned.Error);
        }

        if (owned.Value.IsProfile)
        {
            return Result.Failure<Unit>(TripsErrors.ProfileCannotBeDeleted);
        }

        await travelers.DeleteAsync(command.Id, cancellationToken);
        return Result.Success();
    }

    #endregion Public Methods

    #region Types

    /// <summary>Which traveler to delete.</summary>
    /// <param name="Caller">The user who asks.</param>
    /// <param name="Id">The traveler.</param>
    public sealed record Command(UserId Caller, TravelerId Id);

    #endregion Types
}
