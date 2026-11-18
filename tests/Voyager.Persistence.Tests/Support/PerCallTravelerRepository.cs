using Voyager.Core.Shared;
using Voyager.Core.Trips;
using Voyager.Persistence.Trips;

namespace Voyager.Persistence.Tests.Support;

/// <summary>A traveler repository that takes a new context for each call, like each request of the server.</summary>
internal sealed class PerCallTravelerRepository(TestDatabase database) : ITravelerRepository
{
    public async Task DeleteAsync(TravelerId id, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        await new TravelerRepository(context).DeleteAsync(id, cancellationToken);
    }

    public async Task<Traveler?> FindAsync(TravelerId id, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TravelerRepository(context).FindAsync(id, cancellationToken);
    }

    public async Task<Traveler?> FindProfileAsync(UserId owner, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TravelerRepository(context).FindProfileAsync(owner, cancellationToken);
    }

    public async Task<IReadOnlyList<Traveler>> ListByIdsAsync(IReadOnlyCollection<TravelerId> ids, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TravelerRepository(context).ListByIdsAsync(ids, cancellationToken);
    }

    public async Task<IReadOnlyList<Traveler>> ListByOwnerAsync(UserId owner, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TravelerRepository(context).ListByOwnerAsync(owner, cancellationToken);
    }

    public async Task<bool> TryAddAsync(Traveler traveler, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        return await new TravelerRepository(context).TryAddAsync(traveler, cancellationToken);
    }

    public async Task UpdateAsync(Traveler traveler, CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();
        await new TravelerRepository(context).UpdateAsync(traveler, cancellationToken);
    }
}
