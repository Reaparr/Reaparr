namespace Reaparr.Domain;

// TODO find a better method than using a json record for all these folders
public record DownloadTaskDirectory
{
    public required string DownloadRootPath { get; set; }

    public required string DestinationRootPath { get; set; }

    public required string MovieFolder { get; set; }

    public required string TvShowFolder { get; set; }

    public required string SeasonFolder { get; set; }

    public required string MusicArtistFolder { get; set; }

    public required string MusicAlbumFolder { get; set; }

    public required string PhotoAlbumFolder { get; set; }

    public required string OtherVideoFolder { get; set; }

    public required bool KeepCompletedInDownloadFolder { get; set; }
}
