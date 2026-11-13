using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// Logs written to files when Voyager:LogDirectory names a folder (Voyager.ServiceDefaults, ADR-0012), seen through
/// the group server.
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

    // How long the logger of a stopped server may keep its file open.
    private static readonly TimeSpan _releaseTimeout = TimeSpan.FromSeconds(10);

    private string _logDirectory = string.Empty;

    [TestInitialize]
    public void CreateLogDirectory() =>
        _logDirectory = Path.Combine(Path.GetTempPath(), $"voyager log tests [{Guid.NewGuid():N}]");

    [TestCleanup]
    public void DeleteLogDirectory()
    {
        if (Directory.Exists(_logDirectory))
        {
            WhenReleased(() => Directory.Delete(_logDirectory, recursive: true));
        }
    }

    [TestMethod]
    public void Log_WithLogDirectory_IsWrittenToADailyFile()
    {
        using (var factory = new GroupServerFactory(logDirectory: _logDirectory, environment: "Production", developmentIdentity: false))
        {
            _logReason(factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Voyager.GroupServer.Tests"), "a test", null);
        }

        var text = ReadLog();
        text.Should().Contain("[WRN] Voyager.GroupServer.Tests Something to remember: a test");
    }

    [TestMethod]
    public void Log_WithHomeFolderInMessage_WritesItAsTilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using (var factory = new GroupServerFactory(logDirectory: _logDirectory, environment: "Production", developmentIdentity: false))
        {
            _logPath(
                factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Voyager.GroupServer.Tests"),
                Path.Combine(home, "AppData", "Local", "Voyager", "servers.json"),
                null);
        }

        var text = ReadLog();
        text.Should().Contain(Path.Combine("~", "AppData", "Local", "Voyager", "servers.json"));
        text.Should().NotContain(home);
    }

    [TestMethod]
    public void Log_WithHomeFolderInException_WritesItAsTilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using (var factory = new GroupServerFactory(logDirectory: _logDirectory, environment: "Production", developmentIdentity: false))
        {
            var failure = new IOException($"Cannot open {home.Replace('\\', '/')}/AppData/servers.json");
            _logReason(
                factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Voyager.GroupServer.Tests"),
                "a failure",
                failure);
        }

        var text = ReadLog();
        text.Should().Contain("IOException: Cannot open ~/AppData/servers.json");
        text.Should().NotContainEquivalentOf(home);
    }

    [TestMethod]
    public void Log_BelowTheConfiguredLevel_IsNotWritten()
    {
        using (var factory = new GroupServerFactory(logDirectory: _logDirectory, environment: "Production", developmentIdentity: false))
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
        using var factory = new GroupServerFactory(logDirectory: _logDirectory, environment: "Production", developmentIdentity: false);
        var loggers = factory.Services.GetRequiredService<ILoggerFactory>();

        loggers.CreateLogger("Microsoft.AspNetCore.Routing").IsEnabled(LogLevel.Information).Should().BeFalse();
        loggers.CreateLogger("Voyager.GroupServer").IsEnabled(LogLevel.Information).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("logs", DisplayName = "Relative path")]
    [DataRow(@"\logs", DisplayName = "Root without drive")]
    public void Startup_WithLogDirectoryThatIsNotAbsolute_Fails(string directory)
    {
        using var factory = new GroupServerFactory(logDirectory: directory);

        var start = () => factory.Server;

        start.Should().Throw<InvalidOperationException>().WithMessage("*Voyager:LogDirectory*absolute*");
    }

    /// <summary>The text of the day's log file; the test must have disposed of its factory first.</summary>
    private string ReadLog()
    {
        var files = Directory.GetFiles(_logDirectory, "Voyager.GroupServer-*.log");
        files.Should().ContainSingle();

        // The file is complete once the logger lets go of it.
        return WhenReleased(() =>
        {
            using var stream = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.None);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    /// <summary>
    /// Runs an action on the log files once the server lets go of them. The factory stops the host, but the server's own
    /// Program disposes of it, and so of the logger, on its own thread, a moment later.
    /// </summary>
    private static T WhenReleased<T>(Func<T> action)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return action();
            }
            catch (IOException) when (stopwatch.Elapsed < _releaseTimeout)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static void WhenReleased(Action action) => WhenReleased(() =>
    {
        action();
        return true;
    });
}
