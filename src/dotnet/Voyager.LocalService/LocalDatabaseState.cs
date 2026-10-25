namespace Voyager.LocalService;

/// <summary>
/// Whether the local database was prepared at startup. Read by the diagnostics, so that a failed
/// migration shows the database as unavailable instead of merely reachable.
/// </summary>
public sealed class LocalDatabaseState
{
    #region Properties

    /// <summary>True once the database file exists and every migration has been applied.</summary>
    public bool IsReady { get; internal set; }

    #endregion Properties
}
