namespace Voyager.Core.Trips;

/// <summary>
/// What a caller asks for the avatar of a traveler.
/// </summary>
public enum AvatarChangeKind
{
    /// <summary>Leave the avatar as it is, or without one for a new traveler.</summary>
    Keep,

    /// <summary>Take the avatar away.</summary>
    Remove,

    /// <summary>Put another image in its place.</summary>
    Replace,
}
