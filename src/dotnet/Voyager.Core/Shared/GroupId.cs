namespace Voyager.Core.Shared;

/// <summary>
/// The identifier of a group: the people who organize their trips together. Every datum belongs to one (ADR-0018).
/// </summary>
/// <param name="Value">The UUID v7.</param>
public readonly record struct GroupId(Guid Value)
{
    /// <summary>A new identifier.</summary>
    /// <returns>A UUID v7, so that identifiers sort in the order they were made.</returns>
    public static GroupId New() => new(Guid.CreateVersion7());
}
