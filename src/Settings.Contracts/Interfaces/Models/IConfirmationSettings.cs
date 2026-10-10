namespace Reaparr.Settings.Contracts;

public interface IConfirmationSettings
{
    bool AskDownloadMovieConfirmation { get; set; }

    bool AskDownloadTvShowConfirmation { get; set; }

    bool AskDownloadSeasonConfirmation { get; set; }

    bool AskDownloadEpisodeConfirmation { get; set; }

    bool AskDownloadMusicArtistConfirmation { get; set; }

    bool AskDownloadMusicAlbumConfirmation { get; set; }

    bool AskDownloadMusicTrackConfirmation { get; set; }

    bool AskDownloadPhotoAlbumConfirmation { get; set; }

    bool AskDownloadPhotoImageConfirmation { get; set; }

    bool AskDownloadOtherVideosConfirmation { get; set; }
}
