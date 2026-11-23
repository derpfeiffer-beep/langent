namespace Langent.Core.Trigger;

/// <summary>
/// Pure decision logic for the Strg+C+C trigger (SPEC §4.1). Time is supplied by
/// the caller as millisecond timestamps (the App feeds a <see cref="System.Diagnostics.Stopwatch"/>),
/// so the whole detector is testable without Windows or real time.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Clipboard updates less than <c>burstMergeMs</c> after the previous update
/// are merged into a single copy operation (Office fires several updates per Strg+C).</item>
/// <item>A trigger fires when two copy operations start at most <c>windowMs</c> apart
/// and Ctrl was held for both.</item>
/// <item>After a trigger the pairing state is cleared, so a lone third copy does not re-fire.</item>
/// <item>Updates at a timestamp before the suppression point are ignored and leave state untouched.</item>
/// <item>When disabled the detector never fires.</item>
/// </list>
/// </remarks>
public sealed class DoubleCopyDetector
{
    private int _windowMs;
    private int _burstMergeMs;

    private long? _lastUpdateTs;
    private long? _pendingOpStartTs;
    private bool _pendingOpCtrl;
    private long _suppressUntil = long.MinValue;

    public DoubleCopyDetector(int windowMs = 400, int burstMergeMs = 80, bool enabled = true)
    {
        _windowMs = windowMs;
        _burstMergeMs = burstMergeMs;
        Enabled = enabled;
    }

    /// <summary>When false (e.g. paused via the tray), updates never trigger.</summary>
    public bool Enabled { get; set; }

    /// <summary>Updates the timing thresholds (e.g. after the settings change).</summary>
    public void Configure(int windowMs, int burstMergeMs)
    {
        _windowMs = windowMs;
        _burstMergeMs = burstMergeMs;
    }

    /// <summary>
    /// Ignore every update whose timestamp is before <paramref name="untilTsMs"/>.
    /// The App sets this before it writes to the clipboard itself (SPEC §4.1/§4.3).
    /// </summary>
    public void Suppress(long untilTsMs) => _suppressUntil = untilTsMs;

    /// <summary>Clears all pairing/burst state (full reset).</summary>
    public void Reset()
    {
        _lastUpdateTs = null;
        _pendingOpStartTs = null;
        _pendingOpCtrl = false;
    }

    /// <summary>
    /// Feed one clipboard update. Returns true exactly when this update completes a
    /// double-copy and the caller should act.
    /// </summary>
    public bool OnClipboardUpdate(long tsMs, bool ctrlDown)
    {
        if (!Enabled)
        {
            return false;
        }

        if (tsMs < _suppressUntil)
        {
            return false;
        }

        var isNewOperation = _lastUpdateTs is null || (tsMs - _lastUpdateTs.Value) >= _burstMergeMs;
        _lastUpdateTs = tsMs;

        if (!isNewOperation)
        {
            return false;
        }

        if (_pendingOpStartTs is { } previousStart
            && tsMs - previousStart <= _windowMs
            && _pendingOpCtrl
            && ctrlDown)
        {
            // Trigger: clear pairing state but keep _lastUpdateTs so a trailing
            // burst still merges instead of forming a spurious new operation.
            _pendingOpStartTs = null;
            _pendingOpCtrl = false;
            return true;
        }

        _pendingOpStartTs = tsMs;
        _pendingOpCtrl = ctrlDown;
        return false;
    }
}
