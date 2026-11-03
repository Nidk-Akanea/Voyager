using Voyager.PluginHost.Packages;

namespace Voyager.LocalService;

/// <summary>
/// Log messages of the local service host itself (Program.cs) and of its gRPC services.
/// </summary>
internal static partial class LocalServiceLog
{
    #region Public Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "A prepared plugin package could not be examined: {Error}")]
    public static partial void PackageExaminationFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "The plugin package {PackageSha256} is examined: plugin {PluginId} {PluginVersion} can be installed")]
    public static partial void PackageExaminedAccepted(ILogger logger, string packageSha256, string pluginId, string pluginVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "The plugin package {PackageSha256} is examined: it is refused ({PackageRefusal})")]
    public static partial void PackageExaminedRefused(ILogger logger, string packageSha256, PackageRefusal packageRefusal);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The local service failed and stops")]
    public static partial void ServiceFailed(ILogger logger, Exception exception);

    #endregion Public Methods
}
