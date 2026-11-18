namespace Voyager.Core.Shared;

/// <summary>
/// The family of an expected business failure: what the host turns into a status the caller understands.
/// </summary>
public enum ErrorKind
{
    /// <summary>What the caller sent is not valid: <see cref="Error.Code"/> says why.</summary>
    Invalid,

    /// <summary>The thing the caller asks for does not exist, or the caller may not know that it exists.</summary>
    NotFound,

    /// <summary>The caller knows the thing, but may not do this to it.</summary>
    NotAllowed,

    /// <summary>The request contradicts what is already there.</summary>
    Conflict,
}
