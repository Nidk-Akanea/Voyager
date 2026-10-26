using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Voyager.LocalService.Tests;

/// <summary>
/// Logs written to files on the user's machine (Voyager.ServiceDefaults, ADR-0012), seen through the
/// local service.
/// </summary>
[TestClass]
public sealed class FileLoggingTests
{
    private static readonly Action<ILogger, string, Exception?> _logReason =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1), "Something to remember: {Reason}");

    private static readonly Action<ILogger, string, Exception?> _logDebugDetail =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2), "Detail: {Detail}");

    private static readonly Action<ILogger, string, Exception?> _logPath =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3), "Opening {Path}");

    private string _logDirectory = string.Empty;

    [TestInitialize]
    public void CreateLogDirectory() =>
        _logDirectory = Path.Combine(Path.GetTempPath(), $"voyager log tests [{Guid.NewGuid():N}]");

    [TestCleanup]
    public void DeleteLogDirectory()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Log_WithLogDirectory_IsWrittenToADailyFile()
    {
        using (var factory = new LocalServiceFactory(logDirectory: _logDirectory, environment: "Production"))
        {
            _logReason(factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Voyager.LocalService.Tests"), "a test", null);
        }

        var text = ReadLog();
        text.Should().Contain("[WRN] Voyager.LocalService.Tests Something to remember: a test");
    }

    [TestMethod]
    public void Log_WithHomeFolderInMessage_WritesItAsTilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using (var factory = new LocalServiceFactory(logDirectory: _logDirectory, environment: "Production"))
        {
            _logPath(
                factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Voyager.LocalService.Tests"),
                Path.Combine(home, "AppData", "Local", "Voyager", "voyager.db"),
                null);
        }

        var text = ReadLog();
        text.Should().Contain(Path.Combine("~", "AppData", "Local", "Voyager", "voyager.db"));
        text.Should().NotContain(home);
    }

    [TestMethod]
    public void Log_WithHomeFolderInException_WritesItAsTilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using (var factory = new LocalServiceFactory(logDirectory: _logDirectory, environment: "Production"))
        {
            var failure = new IOException($"Cannot open {home.Replace('\\', '/')}/AppData/voyager.db");
            _logReason(
                factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Voyager.LocalService.Tests"),
                "a failure",
                failure);
        }

        var text = ReadLog();
        text.Should().Contain("IOException: Cannot open ~/AppData/voyager.db");
        text.Should().NotContainEquivalentOf(home);
    }

    [TestMethod]
    public void Log_BelowTheConfiguredLevel_IsNotWritten()
    {
        using (var factory = new LocalServiceFactory(logDirectory: _logDirectory, environment: "Production"))
        {
            var logger = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.AspNetCore.Routing");
            _logDebugDetail(logger, "too detailed", null);
            _logReason(logger, "kept", null);
        }

        var text = ReadLog();
        text.Should().Contain("kept");
        text.Should().NotContain("too detailed");
    }

    [TestMethod]
    public void LogLevels_WithLogDirectory_FollowLoggingSettings()
    {
        using var factory = new LocalServiceFactory(logDirectory: _logDirectory, environment: "Production");
        var loggers = factory.Services.GetRequiredService<ILoggerFactory>();

        loggers.CreateLogger("Microsoft.AspNetCore.Routing").IsEnabled(LogLevel.Information).Should().BeFalse();
        loggers.CreateLogger("Voyager.LocalService").IsEnabled(LogLevel.Information).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("logs", DisplayName = "Relative path")]
    [DataRow(@"\logs", DisplayName = "Root without drive")]
    public void Startup_WithLogDirectoryThatIsNotAbsolute_Fails(string directory)
    {
        using var factory = new LocalServiceFactory(logDirectory: directory);

        var start = () => factory.Server;

        start.Should().Throw<InvalidOperationException>().WithMessage("*Voyager:LogDirectory*absolute*");
    }

    /// <summary>The text of the day's log file; the test must have disposed of its factory first.</summary>
    private string ReadLog()
    {
        // The host has stopped and disposed of the logger: everything is on disk.
        var files = Directory.GetFiles(_logDirectory, "Voyager.LocalService-*.log");
        files.Should().ContainSingle();
        return File.ReadAllText(files[0]);
    }
}
