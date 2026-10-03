namespace Reaparr.Settings.Contracts;

public sealed class DownloadScheduleDTO
{
    public bool Enabled { get; set; }

    public Dictionary<string, Dictionary<string, int?>> Days { get; set; } = new();
}
