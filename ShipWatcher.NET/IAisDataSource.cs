using System.Collections.Concurrent;

namespace ShipWatcher.NET;

public interface IAisDataSource : IDisposable
{
    ConcurrentDictionary<long, Vessel> Vessels { get; }
    int MessageCount { get; }
    bool IsConnected { get; }
    string? LastError { get; }
    string SourceName { get; }

    event Action? OnDataUpdated;

    Task ConnectAsync(CancellationToken ct = default);
    void Disconnect();
}
