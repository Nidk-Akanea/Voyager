using NodaTime;

using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Declares a person the user travels with, once, to find them again from one trip to the next. The traveler belongs
/// to the user.
/// </summary>
/// <param name="travelers">Where the travelers are kept.</param>
/// <param name="clock">The clock of the server.</param>
public sealed class DeclareTraveler(ITravelerRepository travelers, IClock clock)
{
    #region Public Methods

    /// <summary>Declares the traveler.</summary>
    /// <param name="command">What the user typed.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The traveler, or why it is refused.</returns>
    public Task<Result<Traveler>> ExecuteAsync(Command command, CancellationToken cancellationToken) =>
        TravelerCreation.CreateAsync(travelers, clock, command.Caller, command.Id, isProfile: false, command.Details, command.Avatar, cancellationToken);

    #endregion Public Methods

    #region Types

    /// <summary>What the user typed about the traveler.</summary>
    /// <param name="Caller">The user, who becomes the owner.</param>
    /// <param name="Id">The identifier of the traveler, created by the caller.</param>
    /// <param name="Details">The texts and the date of birth.</param>
    /// <param name="Avatar">The image to give it.</param>
    public sealed record Command(UserId Caller, TravelerId Id, TravelerDetails Details, AvatarChange Avatar);

    #endregion Types
}
