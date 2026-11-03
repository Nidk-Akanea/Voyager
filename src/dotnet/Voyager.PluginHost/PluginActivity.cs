using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// The trace of the plugins being loaded or called (ADR-0016, decision 6; question Q26): written to a file while the
/// service runs, deleted when it stops normally. Found at the next start, it tells which plugin the service crashed
/// with. A crash at loading designates its plugin for sure, plugins being loaded one after the other; a crash during
/// calls designates a plugin only when it was the only one being called. It helps after an accident: a plugin keeps
/// the rights of the user, and can crash the service while another one is called, or write this file itself.
/// </summary>
/// <remarks>
/// The file is held open by this service alone for as long as it runs, and rewritten in place, only when what it says
/// changes: no temporary file is left behind, an antivirus cannot hold it between two writes, and another service that
/// shares the folder (in development) can neither read a live trace for a crash nor write over it.
/// </remarks>
/// <param name="file">The file of activity; null when plugins cannot be managed, and nothing is traced then.</param>
/// <param name="logger">Where a file that cannot be written is said, once per failure.</param>
public sealed class PluginActivity(string? file, ILogger<PluginActivity> logger) : IDisposable
{
    #region Fields

    // Pauses before a new attempt to write the trace: shorter than those of PluginStore, since the lock
    // is held meanwhile.
    private static readonly TimeSpan[] _retryDelays = [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200)];

    // How many calls each plugin has running: a plugin with two sources may be called twice at once.
    private readonly Dictionary<PluginKey, int> _calls = [];

    private readonly Lock _gate = new();

    // The loaded plugins by identifier: a source is registered under the identifier of its plugin, unique among them.
    private readonly Dictionary<string, PluginKey> _loaded = new(StringComparer.Ordinal);

    private bool _failureLogged;
    private PluginKey? _loading;

    // Whether this service created the file: only then is it this service's to delete.
    private bool _owned;

    private bool _stopped;
    private FileStream? _stream;

    // What the file says now; null when it must be written again (never written, or the last write failed).
    private string? _written;

    #endregion Fields

    #region Private Methods

    // A trace that was read, or cannot be: gone, it is neither read nor said unreadable again at the next start.
    private static void DeleteQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Held by another program: the first write of this run replaces it.
        }
    }

    // Called under the lock, after every change: the content is written only when it differs from the file's.
    private void Write()
    {
        if (file is null || _stopped)
        {
            return;
        }

        var content = PluginStoreFiles.ActivityText(
            _loading,
            _calls.Keys.OrderBy(plugin => plugin.Origin).ThenBy(plugin => plugin.Id, StringComparer.Ordinal));
        if (content == _written)
        {
            return;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                WriteNow(content);
                _written = content;
                _failureLogged = false;
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Another program (an antivirus) may hold the file for a moment: a short wait, then again.
                if (attempt < _retryDelays.Length)
                {
                    Thread.Sleep(_retryDelays[attempt]);
                    continue;
                }

                // Written again at the next change. Meanwhile, a crash designates no plugin, or the one written before.
                _written = null;
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    PluginHostLog.ActivityNotWritten(logger, FileError.Describe(exception));
                }

                return;
            }
        }
    }

    // Opens the file the first time, then rewrites it in place.
    private void WriteNow(string content)
    {
        if (_stream is null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            // This service's alone: another one that shares the folder cannot take it for the trace of a crash.
            _stream = new FileStream(file!, FileMode.Create, FileAccess.Write, FileShare.None);
            _owned = true;
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        _stream.SetLength(0);
        _stream.Write(bytes);

        // Into the system's hands: it survives the death of the process, which is all a crash needs.
        _stream.Flush();
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Finds the plugin a crash designates, from the trace the previous run left, and deletes the trace.
    /// </summary>
    /// <param name="file">The file of activity; null when plugins cannot be managed.</param>
    /// <param name="logger">Where the outcome is written.</param>
    /// <returns>
    /// The plugin being loaded when the service stopped, or else the only plugin being called; null when the service
    /// stopped normally, the trace names several plugins or none, or another service holds it while it runs.
    /// </returns>
    public static PluginKey? FindSuspect(string? file, ILogger logger)
    {
        if (file is null || !File.Exists(file))
        {
            return null;
        }

        PluginKey? loading;
        IReadOnlyList<PluginKey> calls;
        try
        {
            (loading, calls) = PluginStoreFiles.ReadActivity(file);
        }
        catch (IOException exception) when (FileError.IsSharingViolation(exception))
        {
            // Another service that shares the folder runs, and traces its own plugins: nothing of it is a crash.
            PluginHostLog.ActivityHeldByAnotherService(logger);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or InvalidDataException)
        {
            PluginHostLog.ActivityUnreadable(logger, FileError.Describe(exception));
            DeleteQuietly(file);
            return null;
        }

        // Deleted once read, whatever it says: a trace that cannot be deleted still designates its plugin.
        DeleteQuietly(file);
        if (loading is not null)
        {
            PluginHostLog.CrashDuringLoading(logger, loading.Id, loading.Origin);
            return loading;
        }

        if (calls.Count == 1)
        {
            PluginHostLog.CrashDuringCall(logger, calls[0].Id, calls[0].Origin);
            return calls[0];
        }

        if (calls.Count > 1)
        {
            PluginHostLog.CrashWithoutSuspect(logger, calls.Count);
        }

        return null;
    }

    /// <summary>
    /// Notes that a source of a plugin is being called.
    /// </summary>
    /// <param name="pluginId">The identifier the source is registered under.</param>
    /// <returns>Whether the call is traced: never for the service's own sources.</returns>
    public bool BeginCall(string pluginId)
    {
        lock (_gate)
        {
            if (!_loaded.TryGetValue(pluginId, out var plugin))
            {
                return false;
            }

            _calls[plugin] = _calls.GetValueOrDefault(plugin) + 1;
            Write();
            return true;
        }
    }

    /// <summary>
    /// Notes that a plugin starts loading: its code runs from here on.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    public void BeginLoad(PluginKey plugin)
    {
        lock (_gate)
        {
            _loading = plugin;
            Write();
        }
    }

    /// <summary>
    /// Lets go of the file without deleting it: only <see cref="Stop"/>, at a normal stop, says that nothing crashed.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    /// <summary>
    /// Notes that a call traced by <see cref="BeginCall"/> ended, or outlived its time limit.
    /// </summary>
    /// <param name="pluginId">The identifier the source is registered under.</param>
    public void EndCall(string pluginId)
    {
        lock (_gate)
        {
            if (!_loaded.TryGetValue(pluginId, out var plugin) || !_calls.TryGetValue(plugin, out var count))
            {
                return;
            }

            if (count > 1)
            {
                _calls[plugin] = count - 1;
            }
            else
            {
                _calls.Remove(plugin);
            }

            Write();
        }
    }

    /// <summary>
    /// Notes that a plugin finished loading, whatever the outcome.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="loaded">Whether it is now in use, and so may be called.</param>
    public void EndLoad(PluginKey plugin, bool loaded)
    {
        lock (_gate)
        {
            _loading = null;
            if (loaded)
            {
                _loaded[plugin.Id] = plugin;
            }

            Write();
        }
    }

    /// <summary>
    /// Deletes the trace as the service stops normally: calls still running then are not a crash. Nothing is traced
    /// afterwards.
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _stream?.Dispose();
            _stream = null;

            // Only the file this service created: one it never opened may be another service's.
            if (file is not null && _owned)
            {
                DeleteQuietly(file);
            }
        }
    }

    #endregion Public Methods
}