using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// Where the travelers are kept. Implemented by the storage of the server, which several members use at the same time:
/// what a method promises holds even then.
/// </summary>
public interface ITravelerRepository
{
    /// <summary>Deletes a traveler, and takes it out of every trip it is in, as a single change.</summary>
    /// <param name="id">The traveler.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>A task that ends once the traveler is gone.</returns>
    Task DeleteAsync(TravelerId id, CancellationToken cancellationToken);

    /// <summary>Finds a traveler.</summary>
    /// <param name="id">The traveler.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The traveler, or null when there is none.</returns>
    Task<Traveler?> FindAsync(TravelerId id, CancellationToken cancellationToken);

    /// <summary>Finds the profile of a user.</summary>
    /// <param name="owner">The user.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The profile, or null when the user has not made one.</returns>
    Task<Traveler?> FindProfileAsync(UserId owner, CancellationToken cancellationToken);

    /// <summary>Finds several travelers, whoever owns them.</summary>
    /// <param name="ids">The travelers.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>Those that exist, in no particular order.</returns>
    Task<IReadOnlyList<Traveler>> ListByIdsAsync(IReadOnlyCollection<TravelerId> ids, CancellationToken cancellationToken);

    /// <summary>Lists the travelers of a user, profile included.</summary>
    /// <param name="owner">The user.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The travelers, in the order they were declared.</returns>
    Task<IReadOnlyList<Traveler>> ListByOwnerAsync(UserId owner, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps a new traveler, unless it cannot be: the check and the writing are one change, so that two creations at the
    /// same time never both succeed, and none fails with an exception.
    /// </summary>
    /// <param name="traveler">The traveler.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>
    /// True when the traveler is kept; false when its identifier is already used, or when it is a profile and its owner
    /// already has one.
    /// </returns>
    Task<bool> TryAddAsync(Traveler traveler, CancellationToken cancellationToken);

    /// <summary>Keeps the changes of a traveler; the last writing wins.</summary>
    /// <param name="traveler">The traveler, with its identifier.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>A task that ends once the changes are kept.</returns>
    Task UpdateAsync(Traveler traveler, CancellationToken cancellationToken);
}
