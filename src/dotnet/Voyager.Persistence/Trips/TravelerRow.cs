using Voyager.Core.Trips;

namespace Voyager.Persistence.Trips;

/// <summary>
/// A traveler as the table keeps it. Not the entity of the core: the conversions between the two are written by hand
/// (<see cref="TripsMapping"/>).
/// </summary>
internal sealed class TravelerRow
{
    public byte[]? AvatarContent { get; set; }

    public AvatarFormat? AvatarFormat { get; set; }

    public DateOnly? BirthDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? Description { get; set; }

    public Guid Id { get; set; }

    public bool IsProfile { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Nickname { get; set; }

    public Guid OwnerId { get; set; }
}
