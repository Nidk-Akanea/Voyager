using Microsoft.EntityFrameworkCore;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Persistence.Trips;

/// <summary>
/// The trips, kept in PostgreSQL. Each change touches only what it is about, and is one statement the database runs
/// whole, so that members working at the same time never undo each other.
/// </summary>
/// <param name="database">The database of the request.</param>
public sealed class TripRepository(VoyagerDbContext database) : ITripRepository
{
    #region Public Methods

    /// <inheritdoc />
    public async Task AddTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken)
    {
        database.TripTravelers.Add(new TripTravelerRow { TripId = trip.Value, TravelerId = traveler.Value });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Nothing to add when it is there already, or when the trip or the traveler is gone meanwhile.
            database.ChangeTracker.Clear();
            var nothingToAdd = await database.TripTravelers.AnyAsync(
                    row => row.TripId == trip.Value && row.TravelerId == traveler.Value,
                    cancellationToken)
                || !await database.Trips.AnyAsync(row => row.Id == trip.Value, cancellationToken)
                || !await database.Travelers.AnyAsync(row => row.Id == traveler.Value, cancellationToken);
            if (!nothingToAdd)
            {
                throw;
            }
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TripId id, CancellationToken cancellationToken) =>
        // The rows of its travelers go with it, by the foreign key; the travelers themselves stay.
        await database.Trips.Where(row => row.Id == id.Value).ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Trip?> FindAsync(TripId id, CancellationToken cancellationToken)
    {
        var trips = await LoadAsync(database.Trips.Where(row => row.Id == id.Value), cancellationToken);
        return trips.Count == 0 ? null : trips[0];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Trip>> ListByGroupAsync(GroupId group, CancellationToken cancellationToken) =>
        LoadAsync(database.Trips.Where(row => row.GroupId == group.Value), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Trip>> ListByTravelerAsync(TravelerId traveler, CancellationToken cancellationToken) =>
        LoadAsync(
            database.Trips.Where(row => database.TripTravelers.Any(link => link.TripId == row.Id && link.TravelerId == traveler.Value)),
            cancellationToken);

    /// <inheritdoc />
    public async Task RemoveTravelerAsync(TripId trip, TravelerId traveler, CancellationToken cancellationToken) =>
        await database.TripTravelers
            .Where(row => row.TripId == trip.Value && row.TravelerId == traveler.Value)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task RenameAsync(TripId trip, string name, CancellationToken cancellationToken) =>
        await database.Trips.Where(row => row.Id == trip.Value)
            .ExecuteUpdateAsync(changes => changes.SetProperty(row => row.Name, name), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Trip trip, CancellationToken cancellationToken)
    {
        // The trip and its travelers are one change, but the travelers are written one statement at a time: EF Core would
        // sort the rows of a single batch by key, and the order they were added in would be lost.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            database.Trips.Add(TripsMapping.ToRow(trip));
            await database.SaveChangesAsync(cancellationToken);
            foreach (var traveler in trip.Travelers)
            {
                database.TripTravelers.Add(new TripTravelerRow { TripId = trip.Id.Value, TravelerId = traveler.Value });
                await database.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Rolled back first: a failed statement leaves PostgreSQL refusing the rest of its transaction.
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();

            // A conflict when the identifier is used; anything else is a real failure.
            if (await database.Trips.AnyAsync(row => row.Id == trip.Id.Value, cancellationToken))
            {
                return false;
            }

            throw;
        }
        finally
        {
            database.ChangeTracker.Clear();
        }
    }

    #endregion Public Methods

    #region Private Methods

    // The trips of a query, oldest first, each with its travelers in the order they were added: two readings, not one per trip.
    private async Task<IReadOnlyList<Trip>> LoadAsync(IQueryable<TripRow> query, CancellationToken cancellationToken)
    {
        var rows = await query.AsNoTracking().OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(row => row.Id).ToArray();
        var links = await database.TripTravelers.AsNoTracking()
            .Where(link => ids.Contains(link.TripId))
            .OrderBy(link => link.AddedAt)
            .ThenBy(link => link.TravelerId)
            .ToListAsync(cancellationToken);
        var travelersOf = links.ToLookup(link => link.TripId, link => link.TravelerId);

        return [.. rows.Select(row => TripsMapping.ToTrip(row, travelersOf[row.Id]))];
    }

    #endregion Private Methods
}
