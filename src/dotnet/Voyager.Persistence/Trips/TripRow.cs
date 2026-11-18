namespace Voyager.Persistence.Trips;

/// <summary>
/// A trip as the table keeps it. Its travelers are in <see cref="TripTravelerRow"/>.
/// </summary>
internal sealed class TripRow
{
    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatorId { get; set; }

    public Guid GroupId { get; set; }

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
