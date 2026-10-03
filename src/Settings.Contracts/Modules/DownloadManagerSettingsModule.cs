namespace Reaparr.Settings.Contracts;

public record DownloadManagerSettingsModule
    : BaseSettingsModule<DownloadManagerSettingsModule>,
        IBaseSettingsModule<DownloadManagerSettingsModule>,
        IDownloadManagerSettings
{
    public static DownloadManagerSettingsModule Create() =>
        new() { DownloadSegments = 4, KeepCompletedInDownloadFolder = false };

    public required int DownloadSegments
    {
        get;
        set => SetProperty(ref field, value);
    } = 4;

    public required bool KeepCompletedInDownloadFolder
    {
        get;
        set => SetProperty(ref field, value);
    }

    public DownloadSchedule DownloadSchedule
    {
        get;
        set => SetProperty(ref field, value);
    } = new();
}
