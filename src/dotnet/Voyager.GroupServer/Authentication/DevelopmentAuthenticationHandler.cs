using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Voyager.GroupServer.Authentication;

/// <summary>
/// Signs every call from this machine in as the development account, without asking anything. Registered only when
/// <see cref="GroupServerOptions.DevelopmentIdentity"/> is set in the Development environment (ADR-0019, decision 9).
/// </summary>
/// <remarks>
/// A call from another machine is not signed in, even then: a server started in Development by mistake, listening on
/// every interface, must not let the network in on behalf of the development account. A call with no remote address
/// comes from inside the process (the in-memory server of the tests).
/// </remarks>
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
        var remote = Context.Connection.RemoteIpAddress;
        if (remote is not null && !IPAddress.IsLoopback(remote))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim(DevelopmentIdentity.SUBJECT_CLAIM, DevelopmentIdentity.AccountId.ToString())],
            DevelopmentIdentity.SCHEME);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), DevelopmentIdentity.SCHEME)));
    }

    #endregion Protected Methods
}
