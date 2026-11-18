using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// The trips of the test, with another member who changes one right after a use case read it: what happens when two
/// members work at the same time, which the plain fake never shows.
/// </summary>
/// <param name="inner">The fake that keeps the trips.</param>
internal sealed class ChangingTripRepository(InMemoryTripRepository inner) : ITripRepository
{
    #region Properties

    /// <summary>What another member does right after the next reading of a trip; it happens once.</summary>
    public Func<Task>? AfterNextFind { get; set; }

    #endregion Properties

    #region Public Methods

    public Task AddTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken) =>
        inner.AddTravelerAsync(trip, traveler, cancellationToken);

    public Task DeleteAsync(TripId id, CancellationToken cancellationToken) => inner.DeleteAsync(id, cancellationToken);

    public async Task<Trip?> FindAsync(TripId id, CancellationToken cancellationToken)
    {
        var trip = await inner.FindAsync(id, cancellationToken);
        var change = AfterNextFind;
        AfterNextFind = null;
        if (change is not null)
        {
            await change();
        }

        return trip;
    }

    public Task<IReadOnlyList<Trip>> ListByGroupAsync(GroupId group, CancellationToken cancellationToken) =>
        inner.ListByGroupAsync(group, cancellationToken);

    public Task<IReadOnlyList<Trip>> ListByTravelerAsync(TravelerId traveler, CancellationToken cancellationToken) =>
        inner.ListByTravelerAsync(traveler, cancellationToken);

    public Task RemoveTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken) =>
        inner.RemoveTravelerAsync(trip, traveler, cancellationToken);

    public Task RenameAsync(TripId trip, string name, CancellationToken cancellationToken) =>
        inner.RenameAsync(trip, name, cancellationToken);

    public Task<bool> TryAddAsync(Trip trip, CancellationToken cancellationToken) => inner.TryAddAsync(trip, cancellationToken);

    #endregion Public Methods
}
