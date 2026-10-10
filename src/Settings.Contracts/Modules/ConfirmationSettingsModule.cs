namespace Reaparr.Settings.Contracts;

public record ConfirmationSettingsModule
    : BaseSettingsModule<ConfirmationSettingsModule>,
        IBaseSettingsModule<ConfirmationSettingsModule>,
        IConfirmationSettings
{
    public static ConfirmationSettingsModule Create() =>
        new()
        {
            AskDownloadMovieConfirmation = true,
            AskDownloadTvShowConfirmation = true,
            AskDownloadSeasonConfirmation = true,
            AskDownloadEpisodeConfirmation = true,
            AskDownloadMusicArtistConfirmation = true,
            AskDownloadMusicAlbumConfirmation = true,
            AskDownloadMusicTrackConfirmation = true,
            AskDownloadPhotoAlbumConfirmation = true,
            AskDownloadPhotoImageConfirmation = true,
            AskDownloadOtherVideosConfirmation = true,
        };

    /// <summary>
    /// Indicates whether to ask for confirmation before downloading a movie.
    /// </summary>
    public required bool AskDownloadMovieConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>
    /// Indicates whether to ask for confirmation before downloading a TV show.
    /// </summary>
    public required bool AskDownloadTvShowConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>
    /// Indicates whether to ask for confirmation before downloading a season.
    /// </summary>
    public required bool AskDownloadSeasonConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    /// <summary>
    /// Indicates whether to ask for confirmation before downloading an episode.
    /// </summary>
    public required bool AskDownloadEpisodeConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool AskDownloadMusicArtistConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool AskDownloadMusicAlbumConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool AskDownloadMusicTrackConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool AskDownloadPhotoAlbumConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool AskDownloadPhotoImageConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool AskDownloadOtherVideosConfirmation
    {
        get;
        set => SetProperty(ref field, value);
    } = true;
}
