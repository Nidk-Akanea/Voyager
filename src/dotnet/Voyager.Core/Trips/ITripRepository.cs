using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// Where the trips are kept. Implemented by the storage of the server, which several members use at the same time: what a
/// method promises holds even then. A change touches only what it is about, so that one member's change never undoes
/// another's.
/// </summary>
public interface ITripRepository
{
    /// <summary>
    /// Adds a traveler to a trip, after those already in it, as a single change: two members adding at the same time both
    /// stay, and a traveler already in the trip is left where it is.
    /// </summary>
    /// <param name="trip">The trip. Nothing happens when it is gone.</param>
    /// <param name="traveler">The traveler.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>A task that ends once the traveler is in the trip.</returns>
    Task AddTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken);

    /// <summary>Deletes a trip. Its travelers stay.</summary>
    /// <param name="id">The trip.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>A task that ends once the trip is gone.</returns>
    Task DeleteAsync(TripId id, CancellationToken cancellationToken);

    /// <summary>Finds a trip.</summary>
    /// <param name="id">The trip.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The trip, or null when there is none.</returns>
    Task<Trip?> FindAsync(TripId id, CancellationToken cancellationToken);

    /// <summary>Lists the trips of a group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The trips, in the order they were created.</returns>
    Task<IReadOnlyList<Trip>> ListByGroupAsync(GroupId group, CancellationToken cancellationToken);

    /// <summary>Lists the trips a traveler is in, in every group.</summary>
    /// <param name="traveler">The traveler.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The trips, in the order they were created.</returns>
    Task<IReadOnlyList<Trip>> ListByTravelerAsync(TravelerId traveler, CancellationToken cancellationToken);

    /// <summary>Takes a traveler out of a trip as a single change; the others keep their order.</summary>
    /// <param name="trip">The trip. Nothing happens when it is gone.</param>
    /// <param name="traveler">The traveler. Nothing happens when it is not in the trip.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>A task that ends once the traveler is out of the trip.</returns>
    Task RemoveTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken);

    /// <summary>Changes the name of a trip, and only that: its travelers are not touched. The last writing wins.</summary>
    /// <param name="trip">The trip. Nothing happens when it is gone.</param>
    /// <param name="name">The new name, already checked.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>A task that ends once the name is changed.</returns>
    Task RenameAsync(TripId trip, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps a new trip, unless its identifier is used: the check and the writing are one change, so that two creations at
    /// the same time never both succeed, and none fails with an exception.
    /// </summary>
    /// <param name="trip">The trip, with the travelers it starts with, which must exist: the storage refuses the trip otherwise.</param>
    /// <param name="cancellationToken">Stops the writing.</param>
    /// <returns>True when the trip is kept; false when its identifier is already used.</returns>
    Task<bool> TryAddAsync(Trip trip, CancellationToken cancellationToken);
}
