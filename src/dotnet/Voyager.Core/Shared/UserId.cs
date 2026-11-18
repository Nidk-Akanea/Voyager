namespace Voyager.Core.Shared;

/// <summary>
/// The identifier of a user: the account on the server (ADR-0019), whose identifier is the same on every device.
/// </summary>
/// <param name="Value">The UUID v7.</param>
public readonly record struct UserId(Guid Value)
{
    /// <summary>A new identifier.</summary>
    /// <returns>A UUID v7, so that identifiers sort in the order they were made.</returns>
    public static UserId New() => new(Guid.CreateVersion7());
}
