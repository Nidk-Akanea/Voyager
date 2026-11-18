using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Voyager.Persistence.Trips;

/// <summary>
/// The table of the trips. The group and the creator are plain identifiers, not links: the groups and the accounts have
/// their own tables later, and a creator who left stays the creator (ADR-0019).
/// </summary>
internal sealed class TripConfiguration : IEntityTypeConfiguration<TripRow>
{
    #region Public Methods

    public void Configure(EntityTypeBuilder<TripRow> builder)
    {
        builder.ToTable("trips");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.GroupId).HasColumnName("group_id");
        builder.Property(row => row.CreatorId).HasColumnName("creator_id");
        builder.Property(row => row.Name).HasColumnName("name");
        builder.Property(row => row.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("clock_timestamp()").ValueGeneratedOnAdd();

        // The trips of a group, in the order they were created.
        builder.HasIndex(row => new { row.GroupId, row.CreatedAt }).HasDatabaseName("ix_trips_group_id_created_at");
    }

    #endregion Public Methods
}
