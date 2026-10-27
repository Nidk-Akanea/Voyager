using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// Keeps the log messages written through it, to check what the host and the plugins log.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = [];

    /// <summary>The formatted messages written so far.</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_messages)
            {
                return [.. _messages];
            }
        }
    }

    /// <summary>A logger factory that writes only to this provider.</summary>
    public ILoggerFactory CreateFactory() => LoggerFactory.Create(builder => builder.AddProvider(this));

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
        // Nothing to release.
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (provider._messages)
            {
                provider._messages.Add(formatter(state, exception));
            }
        }
    }
}
