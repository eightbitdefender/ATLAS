namespace ATLAS.Services;

/// <summary>
/// Singleton that holds live NVD sync state. Updated by NvdSyncService,
/// read by VulnerabilitiesController.SyncStatus() via JS polling.
/// </summary>
public class SyncProgressTracker
{
    private readonly object _lock = new();

    public bool     IsRunning    { get; private set; }
    public bool     IsDone       { get; private set; }
    public string   Phase        { get; private set; } = "Idle";
    public int      TotalFetched { get; private set; }
    public int      TotalResults { get; private set; }
    public int      Added        { get; private set; }
    public int      Updated      { get; private set; }
    public string?  Error        { get; private set; }
    public DateTime? StartedAt   { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public int ElapsedSeconds =>
        StartedAt.HasValue
            ? (int)(DateTime.UtcNow - StartedAt.Value).TotalSeconds
            : 0;

    public void Start()
    {
        lock (_lock)
        {
            IsRunning    = true;
            IsDone       = false;
            Error        = null;
            Phase        = "Fetching";
            TotalFetched = 0;
            TotalResults = 0;
            Added        = 0;
            Updated      = 0;
            StartedAt    = DateTime.UtcNow;
            CompletedAt  = null;
        }
    }

    public void UpdateFetch(int fetched, int total)
    {
        lock (_lock)
        {
            TotalFetched = fetched;
            TotalResults = total;
        }
    }

    /// <summary>
    /// Called after each page is committed to the database so the UI
    /// can show a running saved-count alongside the fetch progress.
    /// </summary>
    public void UpdateSaved(int added, int updated)
    {
        lock (_lock)
        {
            Added   = added;
            Updated = updated;
        }
    }

    public void StartSave()
    {
        lock (_lock) { Phase = "Saving"; }
    }

    public void Complete(SyncResult result)
    {
        lock (_lock)
        {
            IsRunning   = false;
            IsDone      = true;
            Added       = result.Added;
            Updated     = result.Updated;
            Error       = result.Error;
            Phase       = result.Success ? "Done" : "Failed";
            CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Returns an anonymous object safe to serialize as JSON for the status endpoint.</summary>
    public object GetStatus() => new
    {
        isRunning    = IsRunning,
        isDone       = IsDone,
        phase        = Phase,
        totalFetched = TotalFetched,
        totalResults = TotalResults,
        added        = Added,
        updated      = Updated,
        error        = Error,
        elapsedSeconds = ElapsedSeconds
    };
}
