using NodaTime;

using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Creates the profile of a user: the traveler the user is, asked at their first connection to a server. A user has a
/// single one.
/// </summary>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="clock">The clock of the server.</param>
public sealed class CreateMyProfile(ITravelerRepository travelers, IClock clock)
{
    #region Public Methods

    /// <summary>Creates the profile.</summary>
    /// <param name="command">What the user typed.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The profile, or why it is refused.</returns>
    public Task<Result<Traveler>> ExecuteAsync(Command command, CancellationToken cancellationToken) =>
        TravelerCreation.CreateAsync(travelers, clock, command.Caller, command.Id, isProfile: true, command.Details, command.Avatar, cancellationToken);

    #endregion Public Methods

    #region Types

    /// <summary>What the user typed about themselves.</summary>
    /// <param name="Caller">The user.</param>
    /// <param name="Id">The identifier of the profile, created by the caller.</param>
    /// <param name="Details">The texts and the date of birth.</param>
    /// <param name="Avatar">The image to give it.</param>
    public sealed record Command(UserId Caller, TravelerId Id, TravelerDetails Details, AvatarChange Avatar);

    #endregion Types
}
