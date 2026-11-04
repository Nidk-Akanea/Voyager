using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Voyager.GroupServer.Authentication;

/// <summary>
/// Signs every call in as the development account of the development group, without asking anything. Registered only
/// when <see cref="GroupServerOptions.DevelopmentIdentity"/> is set in the Development environment (ADR-0019,
/// decision 9).
/// </summary>
/// <param name="options">The options of the scheme.</param>
/// <param name="logger">The logger factory of the scheme.</param>
/// <param name="encoder">The URL encoder of the scheme.</param>
public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    #region Protected Methods

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(DevelopmentIdentity.SUBJECT_CLAIM, DevelopmentIdentity.AccountId.ToString()),
                new Claim(DevelopmentIdentity.GROUP_CLAIM, DevelopmentIdentity.GroupId.ToString()),
            ],
            DevelopmentIdentity.SCHEME);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), DevelopmentIdentity.SCHEME)));
    }

    #endregion Protected Methods
}
