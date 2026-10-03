namespace Reaparr.Settings.Contracts;

public class DownloadManagerSettingsDTO : IDownloadManagerSettings
{
    public required int DownloadSegments { get; set; }

    public required bool KeepCompletedInDownloadFolder { get; set; }

    public DownloadScheduleDTO DownloadSchedule { get; set; } = new();
}
