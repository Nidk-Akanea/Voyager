using Voyager.Core.Shared;

namespace Voyager.Core.Trips.UseCases;

/// <summary>
/// Lists the travelers of a user: the ones they declared, and their profile.
/// </summary>
/// <param name="travelers">Where the travelers are kept.</param>
public sealed class ListMyTravelers(ITravelerRepository travelers)
{
    #region Public Methods

    /// <summary>Lists the travelers.</summary>
    /// <param name="command">Whose travelers.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The travelers, in the order they were declared.</returns>
    public Task<IReadOnlyList<Traveler>> ExecuteAsync(Command command, CancellationToken cancellationToken) =>
        travelers.ListByOwnerAsync(command.Caller, cancellationToken);

    #endregion Public Methods

    #region Types

    /// <summary>Whose travelers to list.</summary>
    /// <param name="Caller">The user, who sees their own travelers only.</param>
    public sealed record Command(UserId Caller);

    #endregion Types
}
