using Voyager.Core.Shared;
using Voyager.Core.Trips;
using Voyager.Persistence.Trips;

namespace Voyager.Persistence.Tests.Support;

/// <summary>A trip repository that takes a new context for each call, like each request of the server.</summary>
internal sealed class PerCallTripRepository(TestDatabase database) : ITripRepository
{
    public async Task AddTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        await new TripRepository(context).AddTravelerAsync(trip, traveler, cancellationToken);
    }

    public async Task DeleteAsync(TripId id, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        await new TripRepository(context).DeleteAsync(id, cancellationToken);
    }

    public async Task<Trip?> FindAsync(TripId id, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TripRepository(context).FindAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> ListByGroupAsync(GroupId group, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TripRepository(context).ListByGroupAsync(group, cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> ListByTravelerAsync(TravelerId traveler, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TripRepository(context).ListByTravelerAsync(traveler, cancellationToken);
    }

    public async Task RemoveTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        await new TripRepository(context).RemoveTravelerAsync(trip, traveler, cancellationToken);
    }

    public async Task RenameAsync(TripId trip, string name, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        await new TripRepository(context).RenameAsync(trip, name, cancellationToken);
    }

    public async Task<bool> TryAddAsync(Trip trip, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TripRepository(context).TryAddAsync(trip, cancellationToken);
    }
}
