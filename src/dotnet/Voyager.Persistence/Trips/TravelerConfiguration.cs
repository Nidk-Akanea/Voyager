using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Voyager.Persistence.Trips;

/// <summary>
/// The table of the travelers. The owner is a plain identifier, not a link to an account table: when an account is
/// deleted its travelers may stay, anonymized, because a trip of a group still cites them (ADR-0019, decision 8).
/// </summary>
internal sealed class TravelerConfiguration : IEntityTypeConfiguration<TravelerRow>
{
    #region Public Methods

    public void Configure(EntityTypeBuilder<TravelerRow> builder)
    {
        builder.ToTable("travelers");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.OwnerId).HasColumnName("owner_id");
        builder.Property(row => row.IsProfile).HasColumnName("is_profile");
        builder.Property(row => row.Name).HasColumnName("name");
        builder.Property(row => row.Nickname).HasColumnName("nickname");
        builder.Property(row => row.BirthDate).HasColumnName("birth_date");
        builder.Property(row => row.Description).HasColumnName("description");
        builder.Property(row => row.AvatarContent).HasColumnName("avatar_content");
        builder.Property(row => row.AvatarFormat).HasColumnName("avatar_format");

        // Given by the database when the row is written, so that the order of declaration is the order of the writings.
        // clock_timestamp(), not now(): now() is the start of the transaction, the same for every row it writes.
        builder.Property(row => row.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("clock_timestamp()").ValueGeneratedOnAdd();

        // The travelers of a user, in the order they were declared.
        builder.HasIndex(row => new { row.OwnerId, row.CreatedAt }).HasDatabaseName("ix_travelers_owner_id_created_at");

        // A user has a single profile, whatever happens at the same time: the database refuses the second.
        builder.HasIndex(row => row.OwnerId)
            .HasDatabaseName("ux_travelers_profile_per_owner")
            .IsUnique()
            .HasFilter("\"is_profile\"");
    }

    #endregion Public Methods
}
