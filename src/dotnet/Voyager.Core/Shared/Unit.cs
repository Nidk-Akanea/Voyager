namespace Voyager.Core.Shared;

/// <summary>
/// The value of an operation that has nothing to give back: <c>Result&lt;Unit&gt;</c> only says whether it worked.
/// </summary>
public readonly record struct Unit
{
    /// <summary>The only value.</summary>
    public static Unit Value => default;
}
