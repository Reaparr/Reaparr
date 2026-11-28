namespace Reaparr.Application;

/// <summary>Offline nickname vocabulary. Ordering and contents define the display-name mapping; UUIDs remain identity keys.</summary>
internal static class TelemetryNicknameDataset
{
    private static readonly Lazy<HashSet<string>> _adjectives = new(() =>
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Amber",
            "Brisk",
            "Calm",
            "Cedar",
            "Clear",
            "Coral",
            "Gentle",
            "Golden",
            "Jade",
            "Lunar",
            "Quiet",
            "Silver",
            "Solar",
            "Swift",
            "Vivid",
            "Warm",
        }
    );

    private static readonly Lazy<HashSet<string>> _nouns = new(() =>
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Brook",
            "Cedar",
            "Cloud",
            "Coral",
            "Forest",
            "Harbor",
            "Meadow",
            "Moon",
            "Ocean",
            "Pine",
            "River",
            "Sky",
            "Star",
            "Stone",
            "Sun",
            "Willow",
        }
    );

    internal static readonly Lazy<string[]> OrderedAdjectives = new(() =>
        _adjectives.Value.Order(StringComparer.Ordinal).ToArray()
    );
    internal static readonly Lazy<string[]> OrderedNouns = new(() =>
        _nouns.Value.Order(StringComparer.Ordinal).ToArray()
    );
    internal static readonly Lazy<string[]> OrderedSpecies = new(() =>
        TelemetrySpecies.Names.Value.Order(StringComparer.Ordinal).ToArray()
    );
}
