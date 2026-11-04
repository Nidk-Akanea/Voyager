namespace Voyager.GroupServer.Authentication;

/// <summary>
/// The fixed account on behalf of which the server accepts calls without signing in, in development under Aspire only
/// (ADR-0019, decision 9), and the development group it will belong to. Fictitious: they name no real person.
/// </summary>
/// <remarks>
/// Raw identifiers until the core has accounts and groups. The group is not carried by the identity: a user has
/// several groups, and the group of a call is given explicitly by the contract (ADR-0020, decision 2).
/// </remarks>
public static class DevelopmentIdentity
{
    #region Fields

    /// <summary>The authentication scheme of the development identity.</summary>
    public const string SCHEME = "Development";

    /// <summary>The claim that carries the account of the caller (the OpenID Connect subject).</summary>
    public const string SUBJECT_CLAIM = "sub";

    /// <summary>The development account.</summary>
    public static readonly Guid AccountId = Guid.Parse("01960000-0000-7000-8000-000000000001");

    /// <summary>The development group, which has every plugin enabled.</summary>
    public static readonly Guid GroupId = Guid.Parse("01960000-0000-7000-8000-000000000002");

    #endregion Fields
}
