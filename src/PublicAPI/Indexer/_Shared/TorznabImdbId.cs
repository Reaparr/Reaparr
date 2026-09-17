namespace Reaparr.PublicAPI;

public static class TorznabImdbId
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var numericPart = trimmed.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? trimmed[2..] : trimmed;
        return numericPart.Length > 0 && numericPart.All(character => character is >= '0' and <= '9')
            ? $"tt{numericPart}"
            : null;
    }
}
