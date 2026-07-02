using System.Collections.Concurrent;

namespace ShipWatcher.NET;

public class VesselStore
{
    private readonly ConcurrentDictionary<long, Vessel> _vessels = new();

    public IReadOnlyDictionary<long, Vessel> Vessels => _vessels;

    public int Count => _vessels.Count;

    /// <summary>
    /// Atomically create or update a vessel. The update function receives the
    /// current snapshot and returns a new one (typically via a `with` expression).
    /// </summary>
    public void Upsert(long mmsi, Func<Vessel, Vessel> update)
    {
        _vessels.AddOrUpdate(
            mmsi,
            m => update(new Vessel { MMSI = m }),
            (_, existing) => update(existing));
    }

    /// <summary>
    /// Update a vessel only if it already exists. Used for bulk metadata feeds
    /// that list vessels we may never receive a position for.
    /// </summary>
    public void UpdateIfExists(long mmsi, Func<Vessel, Vessel> update)
    {
        while (_vessels.TryGetValue(mmsi, out var existing))
        {
            if (_vessels.TryUpdate(mmsi, update(existing), existing))
                return;
        }
    }

    /// <summary>Remove vessels not updated within <paramref name="maxAge"/>. Returns the number removed.</summary>
    public int Prune(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var removed = 0;

        foreach (var (mmsi, vessel) in _vessels)
        {
            // KeyValuePair overload only removes if the value is still this exact
            // snapshot, so a vessel updated concurrently is left alone.
            if (vessel.LastUpdate < cutoff &&
                _vessels.TryRemove(KeyValuePair.Create(mmsi, vessel)))
            {
                removed++;
            }
        }

        return removed;
    }

    public List<Vessel> GetAll() => _vessels.Values.ToList();

    public void Clear() => _vessels.Clear();
}
