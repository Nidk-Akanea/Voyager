using Microsoft.EntityFrameworkCore;

using Voyager.Persistence.Trips;

namespace Voyager.Persistence;

/// <summary>
/// The Voyager database of the group server (PostgreSQL, ADR-0018): the trips and the travelers for now.
/// </summary>
/// <param name="options">The provider and connection chosen by the host.</param>
public sealed class VoyagerDbContext(DbContextOptions<VoyagerDbContext> options) : DbContext(options)
{
    #region Properties

    internal DbSet<TravelerRow> Travelers => Set<TravelerRow>();

    internal DbSet<TripRow> Trips => Set<TripRow>();

    internal DbSet<TripTravelerRow> TripTravelers => Set<TripTravelerRow>();

    #endregion Properties

    #region Protected Methods

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TravelerConfiguration());
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        modelBuilder.ApplyConfiguration(new TripTravelerConfiguration());
    }

    #endregion Protected Methods
}
