using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Voyager.Persistence.Trips;

/// <summary>
/// The table of the travelers of the trips: once each per trip, in the order they were added. Deleting a trip or a
/// traveler deletes its rows, in the same change.
/// </summary>
internal sealed class TripTravelerConfiguration : IEntityTypeConfiguration<TripTravelerRow>
{
    #region Public Methods

    public void Configure(EntityTypeBuilder<TripTravelerRow> builder)
    {
        builder.ToTable("trip_travelers");
        builder.HasKey(row => new { row.TripId, row.TravelerId });
        builder.Property(row => row.TripId).HasColumnName("trip_id");
        builder.Property(row => row.TravelerId).HasColumnName("traveler_id");
        builder.Property(row => row.AddedAt).HasColumnName("added_at").HasDefaultValueSql("clock_timestamp()").ValueGeneratedOnAdd();

        builder.HasOne<TripRow>().WithMany().HasForeignKey(row => row.TripId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TravelerRow>().WithMany().HasForeignKey(row => row.TravelerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(row => row.TravelerId).HasDatabaseName("ix_trip_travelers_traveler_id");
    }

    #endregion Public Methods
}
