using System.Collections.Concurrent;

namespace ShipWatcher.NET.Sources;

public interface IAisDataSource : IDisposable
{
    int MessageCount { get; }
    bool IsConnected { get; }
    string? LastError { get; }
    string SourceName { get; }

    event Action? OnDataUpdated;

    Task ConnectAsync(CancellationToken ct = default);
    void Disconnect();
}
