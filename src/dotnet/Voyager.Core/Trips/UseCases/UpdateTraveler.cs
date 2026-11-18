using NodaTime;

using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Changes what is known of a traveler. Only its owner does: for anyone else, the traveler is not found.
/// </summary>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="clock">The clock of the server.</param>
public sealed class UpdateTraveler(ITravelerRepository travelers, IClock clock)
{
    #region Public Methods

    /// <summary>Changes the traveler.</summary>
    /// <param name="command">What to change.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The changed traveler, or why it is refused.</returns>
    public async Task<Result<Traveler>> ExecuteAsync(Command command, CancellationToken cancellationToken)
    {
        var owned = await TravelerAccess.FindOwnedAsync(travelers, command.Caller, command.Id, cancellationToken);
        if (owned.IsFailure)
        {
            return owned;
        }

        var updated = owned.Value.Update(command.Details, command.Avatar, Days.Today(clock));
        if (updated.IsSuccess)
        {
            await travelers.UpdateAsync(updated.Value, cancellationToken);
        }

        return updated;
    }

    #endregion Public Methods

    #region Types

    /// <summary>What to change about a traveler.</summary>
    /// <param name="Caller">The user who asks.</param>
    /// <param name="Id">The traveler.</param>
    /// <param name="Details">The new texts and date of birth.</param>
    /// <param name="Avatar">What to do with the avatar.</param>
    public sealed record Command(UserId Caller, TravelerId Id, TravelerDetails Details, AvatarChange Avatar);

    #endregion Types
}
