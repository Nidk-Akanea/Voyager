namespace Voyager.Core.Trips;

/// <summary>
/// A trip with its travelers in full, as every member of its group sees it.
/// </summary>
/// <param name="Trip">The trip.</param>
/// <param name="Travelers">Its travelers, in the order they were added.</param>
public sealed record TripView(Trip Trip, IReadOnlyList<Traveler> Travelers);
