namespace Voyager.GroupServer.Authentication;

/// <summary>
/// The fixed account and group on behalf of which the server accepts calls without signing in, in development under
/// Aspire only (ADR-0019, decision 9). Fictitious: they name no real person.
/// </summary>
public static class DevelopmentIdentity
{
    #region Fields

    /// <summary>The authentication scheme of the development identity.</summary>
    public const string SCHEME = "Development";

    /// <summary>The claim that carries the group of the caller.</summary>
    public const string GROUP_CLAIM = "voyager_group";

    /// <summary>The claim that carries the account of the caller (the OpenID Connect subject).</summary>
    public const string SUBJECT_CLAIM = "sub";

    /// <summary>The development account.</summary>
    public static readonly Guid AccountId = Guid.Parse("01960000-0000-7000-8000-000000000001");

    /// <summary>The development group, which has every plugin enabled.</summary>
    public static readonly Guid GroupId = Guid.Parse("01960000-0000-7000-8000-000000000002");

    #endregion Fields
}
