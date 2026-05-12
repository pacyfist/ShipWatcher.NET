using System.Collections.Concurrent;

namespace ShipWatcher.NET;

public class VesselStore
{
    public ConcurrentDictionary<long, Vessel> Vessels { get; } = new();

    public Vessel GetOrAdd(long mmsi)
    {
        return Vessels.GetOrAdd(mmsi, m => new Vessel { MMSI = m });
    }

    public void Upsert(long mmsi, Action<Vessel> updateAction)
    {
        var vessel = GetOrAdd(mmsi);
        lock (vessel)
        {
            updateAction(vessel);
        }
    }

    public List<Vessel> GetAll() => Vessels.Values.ToList();
}
