namespace ShipWatcher.NET.Sources;

/// <summary>
/// Describes a data source's configuration needs so the source selection dialog
/// can generate its UI dynamically.
/// </summary>
public interface ISourceDescriptor
{
    string DisplayLabel { get; }
    IReadOnlyList<SourceConfigField> ConfigFields { get; }
    string? ValidateConfig();
    void ApplyConfig(IReadOnlyDictionary<string, string> values);
}

public record SourceConfigField(string Key, string Label, string CurrentValue, bool IsSensitive = false);
