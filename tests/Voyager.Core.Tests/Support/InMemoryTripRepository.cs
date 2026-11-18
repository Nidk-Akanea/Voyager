using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// The trips of the test, kept in the order they were added, as the port promises. Each change touches only what it is
/// about, as the port promises too.
/// </summary>
internal sealed class InMemoryTripRepository : ITripRepository
{
    #region Fields

    private readonly List<Trip> _trips = [];

    #endregion Fields

    #region Properties

    public IReadOnlyList<Trip> All => _trips;

    #endregion Properties

    #region Public Methods

    public void Add(Trip trip) => _trips.Add(trip);

    public Task AddTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken)
    {
        Change(trip, found => found.WithTraveler(traveler));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TripId id, CancellationToken cancellationToken)
    {
        _trips.RemoveAll(trip => trip.Id == id);
        return Task.CompletedTask;
    }

    public Trip? Find(TripId id) => _trips.Find(trip => trip.Id == id);

    public Task<Trip?> FindAsync(TripId id, CancellationToken cancellationToken) => Task.FromResult(Find(id));

    public Task<IReadOnlyList<Trip>> ListByGroupAsync(GroupId group, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Trip>>([.. _trips.Where(trip => trip.GroupId == group)]);

    public Task<IReadOnlyList<Trip>> ListByTravelerAsync(TravelerId traveler, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Trip>>([.. _trips.Where(trip => trip.Includes(traveler))]);

    public Task RemoveTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken)
    {
        Change(trip, found => found.WithoutTraveler(traveler));
        return Task.CompletedTask;
    }

    public Task RenameAsync(TripId trip, string name, CancellationToken cancellationToken)
    {
        Change(trip, found => found.Rename(name).Value);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (Find(trip.Id) is not null)
        {
            return Task.FromResult(false);
        }

        _trips.Add(trip);
        return Task.FromResult(true);
    }

    #endregion Public Methods

    #region Private Methods

    private void Change(TripId id, Func<Trip, Trip> change)
    {
        var index = _trips.FindIndex(trip => trip.Id == id);
        if (index >= 0)
        {
            _trips[index] = change(_trips[index]);
        }
    }

    #endregion Private Methods
}
