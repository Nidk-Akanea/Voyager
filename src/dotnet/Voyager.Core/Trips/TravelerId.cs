namespace Voyager.Core.Trips;

/// <summary>
/// The identifier of a traveler. Created by the client, as a UUID v7, so that sending a creation again is recognized
/// (ADR-0018, decision 2).
/// </summary>
/// <param name="Value">The UUID v7.</param>
public readonly record struct TravelerId(Guid Value)
{
    /// <summary>A new identifier.</summary>
    /// <returns>A UUID v7, so that identifiers sort in the order they were made.</returns>
    public static TravelerId New() => new(Guid.CreateVersion7());
}
