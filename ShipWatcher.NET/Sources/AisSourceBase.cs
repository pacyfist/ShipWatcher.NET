using Serilog;

namespace ShipWatcher.NET.Sources;

/// <summary>
/// Shared connection lifecycle for AIS data sources: ConnectAsync starts a
/// supervised run loop that keeps the source alive, reconnecting with
/// exponential backoff (1s doubling to 60s) whenever the connection fails or
/// the stream ends. Subclasses implement one connection attempt in
/// <see cref="ReceiveAsync"/> and teardown in <see cref="CleanupConnection"/>.
/// </summary>
public abstract class AisSourceBase : IAisDataSource
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DisconnectWait = TimeSpan.FromSeconds(2);

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private TimeSpan _backoff = InitialBackoff;

    protected abstract ILogger Log { get; }

    public int MessageCount { get; protected set; }
    public string? LastError { get; protected set; }
    public abstract bool IsConnected { get; }
    public abstract string SourceName { get; }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        Disconnect();

        _backoff = InitialBackoff;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;
        _runTask = Task.Run(() => RunAsync(token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ReceiveAsync(ct);
                Log.Information("{Source} stream ended", SourceName);
                LastError ??= "stream ended";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Log.Error(ex, "{Source} connection failed", SourceName);
            }
            finally
            {
                CleanupConnection();
            }

            if (ct.IsCancellationRequested)
                break;

            Log.Information("{Source} reconnecting in {Backoff}", SourceName, _backoff);
            try
            {
                await Task.Delay(_backoff, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
        }
    }

    /// <summary>
    /// One connection attempt: open the connection and process messages until
    /// cancellation or end of stream. Call <see cref="ReportHealthy"/> once
    /// data is actually flowing so the backoff resets.
    /// </summary>
    protected abstract Task ReceiveAsync(CancellationToken ct);

    /// <summary>
    /// Tear down connection-scoped resources. Must be idempotent; also called
    /// from <see cref="Disconnect"/> while <see cref="ReceiveAsync"/> may still
    /// be blocked on I/O, to force it to abort.
    /// </summary>
    protected abstract void CleanupConnection();

    /// <summary>Clears the last error and resets the reconnect backoff.</summary>
    protected void ReportHealthy()
    {
        LastError = null;
        _backoff = InitialBackoff;
    }

    public void Disconnect()
    {
        var cts = _cts;
        var task = _runTask;
        _cts = null;
        _runTask = null;

        if (cts is null)
        {
            CleanupConnection();
            return;
        }

        try
        {
            cts.Cancel();
            CleanupConnection();
            task?.Wait(DisconnectWait);
        }
        catch (AggregateException)
        {
            // Run loop surfaced its cancellation; nothing to do.
        }
        finally
        {
            cts.Dispose();
        }
    }

    public virtual void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
