using Microsoft.Extensions.Logging;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// Keeps the exception the server logs when it fails and stops (GroupServerLog.ServerFailed). A test reads it there rather
/// than from what the in-memory host throws: when the server stops, it disposes its host while the factory may still be
/// starting it, and the factory then throws an <see cref="ObjectDisposedException"/> instead of the cause.
/// </summary>
internal sealed class StartupFailureLog : ILoggerProvider
{
    #region Fields

    private readonly Logger _logger;

    #endregion Fields

    #region Public Constructors

    public StartupFailureLog() => _logger = new Logger(this);

    #endregion Public Constructors

    #region Properties

    /// <summary>The exception of the last critical log entry; null when the server did not fail.</summary>
    public Exception? Failure { get; private set; }

    #endregion Properties

    #region Public Methods

    public ILogger CreateLogger(string categoryName) => _logger;

    public void Dispose()
    {
    }

    #endregion Public Methods

    #region Classes

    private sealed class Logger(StartupFailureLog log) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Critical;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Critical && exception is not null)
            {
                log.Failure = exception;
            }
        }
    }

    #endregion Classes
}
