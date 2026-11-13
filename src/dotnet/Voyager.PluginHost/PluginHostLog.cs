using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// Log messages of the plugin host. Plugin identifiers are technical and safe to log; folder names that
/// are not valid identifiers, paths and manifest content are never written as text. The exceptions of a
/// plugin are written with their message: a plugin is trusted code, and the file output of the server
/// replaces the home folder of the account by "~" in messages and exceptions alike.
/// </summary>
internal static partial class PluginHostLog
{
    #region Public Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} uses contract {Contract} version {ContractVersion}, which a later release will remove")]
    public static partial void ContractDeprecated(ILogger logger, string pluginId, string contract, int contractVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is set aside: it uses contract {Contract} version {ContractVersion}, which this application does not support ({ContractState})")]
    public static partial void ContractNotSupported(ILogger logger, string pluginId, string contract, int contractVersion, ContractUseState contractState);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The facts of {SourceId} were collected: {Count} kept")]
    public static partial void FactSourceCollected(ILogger logger, string sourceId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The facts of {SourceId} failed")]
    public static partial void FactSourceFailed(ILogger logger, string sourceId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The facts of {SourceId} are invalid: {Problem}")]
    public static partial void FactSourceInvalid(ILogger logger, string sourceId, string problem);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The facts of {SourceId} are not asked again: its previous call is still running")]
    public static partial void FactSourceStillRunning(ILogger logger, string sourceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The facts of {SourceId} took more than {TimeoutMilliseconds} ms and are left out")]
    public static partial void FactSourceTimedOut(ILogger logger, string sourceId, double timeoutMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The source {SourceId} gave {Count} facts: only the first {Maximum} are kept")]
    public static partial void FactSourceTruncated(ILogger logger, string sourceId, int count, int maximum);

    [LoggerMessage(Level = LogLevel.Information, Message = "No plugin folder is configured: no plugin is loaded")]
    public static partial void NoPluginFolder(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The container of plugin {PluginId} did not shut down cleanly")]
    public static partial void PluginContainerDisposeFailed(ILogger logger, string pluginId, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} ({PluginOrigin}) is disabled: it is not loaded")]
    public static partial void PluginDisabled(ILogger logger, string pluginId, PluginOrigin pluginOrigin);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is set aside: its folder name is not its identifier")]
    public static partial void PluginFolderNameMismatch(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A plugin folder failed unexpectedly while loading and is set aside")]
    public static partial void PluginFolderSkipped(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The plugin folder could not be read: no plugin is loaded")]
    public static partial void PluginFolderUnreadable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is set aside: this identifier is reserved for the host")]
    public static partial void PluginIdReserved(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} {PluginVersion} ({PluginOrigin}) is loaded")]
    public static partial void PluginLoaded(ILogger logger, string pluginId, string pluginVersion, PluginOrigin pluginOrigin);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} failed to load")]
    public static partial void PluginLoadFailed(ILogger logger, string pluginId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is set aside: {Reason}")]
    public static partial void PluginRejected(ILogger logger, string pluginId, PluginRejectionReason reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugins loaded: {LoadedCount} in use, {RejectedCount} set aside, {DisabledCount} disabled, {SuspendedCount} suspended")]
    public static partial void PluginsLoaded(ILogger logger, int loadedCount, int rejectedCount, int disabledCount, int suspendedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} ({PluginOrigin}) is suspended after a crash: it is not loaded until the user decides")]
    public static partial void PluginSuspended(ILogger logger, string pluginId, PluginOrigin pluginOrigin);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} is set aside: its assembly must hold exactly one public, non-abstract class that implements IPlugin")]
    public static partial void PluginTypeNotFound(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} registered {ExtensionPoint}, which its manifest does not announce")]
    public static partial void UndeclaredRegistration(ILogger logger, string pluginId, string extensionPoint);

    #endregion Public Methods
}
