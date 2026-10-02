namespace Reaparr.Domain;

public record DownloadTaskDirectory
{
    public required string DownloadRootPath { get; set; }

    public required string DestinationRootPath { get; set; }

    public required string MovieFolder { get; set; }

    public required string TvShowFolder { get; set; }

    public required string SeasonFolder { get; set; }

    public string? RelativeDirectory { get; set; }

    // Optional per-task override; when true, keep completed files in the download folder
    public required bool KeepCompletedInDownloadFolder { get; set; }
}
