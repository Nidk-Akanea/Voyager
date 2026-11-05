namespace Voyager.GroupServer;

/// <summary>
/// Log messages about the interface files of the plugins: <see cref="InterfaceFileCatalog"/> as it reads them at startup,
/// <see cref="Api.PluginHostGrpcService"/> as it sends them.
/// </summary>
internal static partial class InterfaceFileLog
{
    #region Public Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "A file of the interface of plugin {PluginId} is left out: its name or its size is not allowed")]
    public static partial void FileLeftOut(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "An interface file of plugin {PluginId} {PluginVersion} could not be read: the desktop app cannot show its panel")]
    public static partial void FileUnreadable(ILogger logger, string pluginId, string pluginVersion, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The interface module of plugin {PluginId} is missing or left out: the plugin is shown without its panel")]
    public static partial void ModuleMissing(ILogger logger, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} has {Count} interface files, more than the {Maximum} allowed: the plugin is shown without its panel")]
    public static partial void TooManyFiles(ILogger logger, string pluginId, int count, int maximum);

    [LoggerMessage(Level = LogLevel.Error, Message = "The interface files of plugin {PluginId} could not be read: the plugin is shown without its panel")]
    public static partial void Unreadable(ILogger logger, string pluginId, Exception exception);

    #endregion Public Methods
}
