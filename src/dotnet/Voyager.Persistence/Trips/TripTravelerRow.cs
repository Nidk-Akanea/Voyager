namespace Voyager.Persistence.Trips;

/// <summary>
/// A traveler in a trip. Its rows go when the trip or the traveler goes; the order of the travelers of a trip is the
/// order they were added.
/// </summary>
internal sealed class TripTravelerRow
{
    public DateTimeOffset AddedAt { get; set; }

    public Guid TravelerId { get; set; }

    public Guid TripId { get; set; }
}
