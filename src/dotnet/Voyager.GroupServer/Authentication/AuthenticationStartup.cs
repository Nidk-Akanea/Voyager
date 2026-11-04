using Microsoft.AspNetCore.Authentication;

namespace Voyager.GroupServer.Authentication;

/// <summary>
/// Wires the authentication of the group server: the development identity when it is allowed, otherwise a scheme that
/// signs nobody in until the real sign-in exists (ADR-0019).
/// </summary>
internal static class AuthenticationStartup
{
    #region Public Methods

    /// <summary>
    /// Registers authentication and authorization. Every gRPC service is mapped with <c>RequireAuthorization</c>.
    /// </summary>
    /// <param name="builder">The builder of the server; its settings and environment decide the scheme.</param>
    /// <returns>The same builder, to chain calls.</returns>
    public static WebApplicationBuilder AddGroupServerAuthentication(this WebApplicationBuilder builder)
    {
        // Read here, before the options are validated: outside Development the setting stops the server at startup
        // (Program.cs), so it can never sign anyone in there.
        var developmentIdentity = builder.Environment.IsDevelopment()
            && builder.Configuration.GetValue<bool>($"{GroupServerOptions.SECTION}:{nameof(GroupServerOptions.DevelopmentIdentity)}");

        if (developmentIdentity)
        {
            builder.Services.AddAuthentication(DevelopmentIdentity.SCHEME)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentIdentity.SCHEME, configureOptions: null);
        }
        else
        {
            builder.Services.AddAuthentication(NoSignInAuthenticationHandler.SCHEME)
                .AddScheme<AuthenticationSchemeOptions, NoSignInAuthenticationHandler>(NoSignInAuthenticationHandler.SCHEME, configureOptions: null);
        }

        builder.Services.AddAuthorization();
        return builder;
    }

    #endregion Public Methods
}
