using Microsoft.Extensions.Options;

namespace Voyager.LocalService;

/// <summary>
/// Ties the service to the desktop app that started it (src/ts/desktop/src/main/localService.ts):
/// the service announces where it listens, and stops when the app goes away.
/// </summary>
internal static partial class DesktopAppLink
{
    #region Fields

    /// <summary>
    /// Start of the line written to the standard output once the service listens, followed by its address.
    /// </summary>
    public const string LISTENING_PREFIX = "VOYAGER_LISTENING ";

    #endregion Fields

    #region Private Methods

    [LoggerMessage(Level = LogLevel.Information, Message = "Standard input closed: the desktop app is gone, stopping the local service")]
    private static partial void LogDesktopAppGone(ILogger logger);

    private static void StopWhenInputCloses(IHostApplicationLifetime lifetime, ILogger logger)
    {
        while (Console.In.ReadLine() is not null)
        {
            // The desktop app writes nothing: only the end of the input matters.
        }

        LogDesktopAppGone(logger);
        lifetime.StopApplication();
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Writes the listening address once the service accepts calls; the desktop app waits for this
    /// line instead of guessing a free port. With <see cref="LocalServiceOptions.StopWhenInputCloses"/>,
    /// also stops the service when its standard input closes: the app quit, or crashed.
    /// </summary>
    /// <param name="app">The web application, not started yet.</param>
    public static void LinkToDesktopApp(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var address = app.Urls.FirstOrDefault();
            if (address is not null)
            {
                Console.Out.WriteLine(LISTENING_PREFIX + address);
                Console.Out.Flush();
            }
        });

        if (app.Services.GetRequiredService<IOptions<LocalServiceOptions>>().Value.StopWhenInputCloses)
        {
            // Reading the console blocks a thread: a dedicated background thread, not the thread pool.
            var watcher = new Thread(() => StopWhenInputCloses(app.Lifetime, app.Logger))
            {
                IsBackground = true,
                Name = "Desktop app watcher",
            };
            watcher.Start();
        }
    }

    #endregion Public Methods
}
