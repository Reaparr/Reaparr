namespace Reaparr.Settings.Contracts;

public class ConfirmationSettingsDTO : IConfirmationSettings
{
    public required bool AskDownloadMovieConfirmation { get; set; }

    public required bool AskDownloadTvShowConfirmation { get; set; }

    public required bool AskDownloadSeasonConfirmation { get; set; }

    public required bool AskDownloadEpisodeConfirmation { get; set; }

    public required bool AskDownloadMusicArtistConfirmation { get; set; }

    public required bool AskDownloadMusicAlbumConfirmation { get; set; }

    public required bool AskDownloadMusicTrackConfirmation { get; set; }

    public required bool AskDownloadPhotoAlbumConfirmation { get; set; }

    public required bool AskDownloadPhotoImageConfirmation { get; set; }

    public required bool AskDownloadOtherVideosConfirmation { get; set; }
}
