namespace Voyager.Sdk;

/// <summary>
/// Information about the version of the SDK that is running.
/// </summary>
public static class SdkInfo
{
    /// <summary>
    /// The version of this SDK, computed by GitVersion and never written by hand. A plugin states the
    /// major and minor version it was written for in the <c>sdk</c> field of its manifest. Until the SDK
    /// has its own version history (<c>sdk/v*</c> tags, ADR-0007), this is the version of the application.
    /// </summary>
    public static Version Version { get; } = typeof(SdkInfo).Assembly.GetName().Version ?? new Version(0, 0);
}
