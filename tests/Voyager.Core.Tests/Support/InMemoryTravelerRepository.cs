using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Core.Tests.Support;

/// <summary>
/// The travelers of the test. Deleting one takes it out of the trips, as the contract of the port says, and a user has
/// at most one profile.
/// </summary>
/// <param name="trips">The trips of the same test, which a deletion changes.</param>
internal sealed class InMemoryTravelerRepository(InMemoryTripRepository trips) : ITravelerRepository
{
    #region Fields

    private readonly List<Traveler> _travelers = [];

    #endregion Fields

    #region Properties

    public IReadOnlyList<Traveler> All => _travelers;

    #endregion Properties

    #region Public Methods

    public void Add(Traveler traveler) => _travelers.Add(traveler);

    public async Task DeleteAsync(TravelerId id, CancellationToken cancellationToken)
    {
        _travelers.RemoveAll(traveler => traveler.Id == id);
        foreach (var trip in trips.All.Where(trip => trip.Includes(id)).ToList())
        {
            await trips.RemoveTravelerAsync(trip.Id, id, cancellationToken);
        }
    }

    public Traveler? Find(TravelerId id) => _travelers.Find(traveler => traveler.Id == id);

    public Task<Traveler?> FindAsync(TravelerId id, CancellationToken cancellationToken) => Task.FromResult(Find(id));

    public Task<Traveler?> FindProfileAsync(UserId owner, CancellationToken cancellationToken) =>
        Task.FromResult(_travelers.Find(traveler => traveler.OwnerId == owner && traveler.IsProfile));

    /// <summary>The port promises no order: the fake answers in reverse so that no code relies on one.</summary>
    public Task<IReadOnlyList<Traveler>> ListByIdsAsync(
        IReadOnlyCollection<TravelerId> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Traveler>>([.. _travelers.Where(traveler => ids.Contains(traveler.Id)).Reverse()]);

    public Task<IReadOnlyList<Traveler>> ListByOwnerAsync(UserId owner, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Traveler>>([.. _travelers.Where(traveler => traveler.OwnerId == owner)]);

    public Task<bool> TryAddAsync(Traveler traveler, CancellationToken cancellationToken)
    {
        var taken = Find(traveler.Id) is not null
            || (traveler.IsProfile && _travelers.Exists(other => other.OwnerId == traveler.OwnerId && other.IsProfile));
        if (taken)
        {
            return Task.FromResult(false);
        }

        _travelers.Add(traveler);
        return Task.FromResult(true);
    }

    public Task UpdateAsync(Traveler traveler, CancellationToken cancellationToken)
    {
        var index = _travelers.FindIndex(other => other.Id == traveler.Id);
        if (index >= 0)
        {
            _travelers[index] = traveler;
        }

        return Task.CompletedTask;
    }

    #endregion Public Methods
}
