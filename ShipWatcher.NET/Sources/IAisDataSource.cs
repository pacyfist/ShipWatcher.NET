namespace ShipWatcher.NET.Sources;

public interface IAisDataSource : IDisposable
{
    int MessageCount { get; }
    bool IsConnected { get; }
    string? LastError { get; }
    string SourceName { get; }

    /// <summary>
    /// Start the source. Returns immediately; the connection is supervised in
    /// the background and reconnects automatically until <see cref="Disconnect"/>.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    void Disconnect();
}
