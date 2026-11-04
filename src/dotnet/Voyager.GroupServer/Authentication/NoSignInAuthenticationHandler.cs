using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Voyager.GroupServer.Authentication;

/// <summary>
/// The scheme of a server that has no way to sign in yet: it authenticates nobody, so that every protected call is
/// refused as unauthenticated. The real sign-in (OpenIddict) replaces it (ADR-0019).
/// </summary>
/// <param name="options">The options of the scheme.</param>
/// <param name="logger">The logger factory of the scheme.</param>
/// <param name="encoder">The URL encoder of the scheme.</param>
public sealed class NoSignInAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    #region Fields

    /// <summary>The name of the scheme.</summary>
    public const string SCHEME = "NoSignIn";

    #endregion Fields

    #region Protected Methods

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

    #endregion Protected Methods
}
