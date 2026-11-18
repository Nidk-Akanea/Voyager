using NodaTime;

namespace Voyager.Core.Trips;

/// <summary>
/// What a user types about a traveler, before it is checked.
/// </summary>
/// <param name="Name">The name: required, 1 to 100 characters.</param>
/// <param name="Nickname">The short name the screens show: optional, 1 to 50 characters.</param>
/// <param name="BirthDate">The date of birth, a calendar date: optional, and not after the day the server is in.</param>
/// <param name="Description">A free text: optional, 500 characters at most, the only one that keeps line breaks.</param>
/// <remarks>
/// Personal data: the text of a record would write them all, in a log or in the message of a failed test. It writes
/// none, and neither does the command that holds it.
/// </remarks>
public sealed record TravelerDetails(string? Name, string? Nickname, LocalDate? BirthDate, string? Description)
{
    /// <summary>The name of the type, and nothing of what it holds.</summary>
    /// <returns>The name of the type.</returns>
    public override string ToString() => nameof(TravelerDetails);
}
