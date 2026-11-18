using Microsoft.EntityFrameworkCore;

using Voyager.Core.Shared;
using Voyager.Core.Trips;

namespace Voyager.Persistence.Trips;

/// <summary>
/// The travelers, kept in PostgreSQL. A creation is arbitrated by the database, not by a reading before the writing: two
/// at the same time never both succeed, and the loser is told rather than failed.
/// </summary>
/// <param name="database">The database of the request.</param>
public sealed class TravelerRepository(VoyagerDbContext database) : ITravelerRepository
{
    #region Public Methods

    /// <inheritdoc />
    public async Task DeleteAsync(TravelerId id, CancellationToken cancellationToken) =>
        // The rows of the trips it is in go with it, by the foreign key: a single change.
        await database.Travelers.Where(row => row.Id == id.Value).ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Traveler?> FindAsync(TravelerId id, CancellationToken cancellationToken)
    {
        var row = await database.Travelers.AsNoTracking().FirstOrDefaultAsync(found => found.Id == id.Value, cancellationToken);
        return row is null ? null : TripsMapping.ToTraveler(row);
    }

    /// <inheritdoc />
    public async Task<Traveler?> FindProfileAsync(UserId owner, CancellationToken cancellationToken)
    {
        var row = await database.Travelers.AsNoTracking()
            .FirstOrDefaultAsync(found => found.OwnerId == owner.Value && found.IsProfile, cancellationToken);
        return row is null ? null : TripsMapping.ToTraveler(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Traveler>> ListByIdsAsync(IReadOnlyCollection<TravelerId> ids, CancellationToken cancellationToken)
    {
        var values = ids.Select(id => id.Value).ToArray();
        var rows = await database.Travelers.AsNoTracking().Where(row => values.Contains(row.Id)).ToListAsync(cancellationToken);
        return [.. rows.Select(TripsMapping.ToTraveler)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Traveler>> ListByOwnerAsync(UserId owner, CancellationToken cancellationToken)
    {
        var rows = await database.Travelers.AsNoTracking()
            .Where(row => row.OwnerId == owner.Value)
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(TripsMapping.ToTraveler)];
    }

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Traveler traveler, CancellationToken cancellationToken)
    {
        database.Travelers.Add(TripsMapping.ToRow(traveler));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The database refused the row. It is a conflict if something explains it: the identifier is used, or the
            // owner has a profile already. Anything else is a real failure.
            database.ChangeTracker.Clear();
            var taken = await database.Travelers.AnyAsync(row => row.Id == traveler.Id.Value, cancellationToken)
                || (traveler.IsProfile && await database.Travelers.AnyAsync(
                    row => row.OwnerId == traveler.OwnerId.Value && row.IsProfile,
                    cancellationToken));
            if (taken)
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

    /// <inheritdoc />
    public async Task UpdateAsync(Traveler traveler, CancellationToken cancellationToken)
    {
        var avatar = traveler.Avatar?.Content.ToArray();
        var avatarFormat = traveler.Avatar?.Format;
        var birthDate = TripsMapping.ToDateOnly(traveler.BirthDate);

        await database.Travelers.Where(row => row.Id == traveler.Id.Value).ExecuteUpdateAsync(
            changes => changes
                .SetProperty(row => row.Name, traveler.Name)
                .SetProperty(row => row.Nickname, traveler.Nickname)
                .SetProperty(row => row.BirthDate, birthDate)
                .SetProperty(row => row.Description, traveler.Description)
                .SetProperty(row => row.AvatarContent, avatar)
                .SetProperty(row => row.AvatarFormat, avatarFormat),
            cancellationToken);
    }

    #endregion Public Methods
}
