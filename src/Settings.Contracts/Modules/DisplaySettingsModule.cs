namespace Reaparr.Settings.Contracts;

public record DisplaySettingsModule
    : BaseSettingsModule<DisplaySettingsModule>,
        IBaseSettingsModule<DisplaySettingsModule>,
        IDisplaySettings
{
    public static DisplaySettingsModule Create() =>
        new()
        {
            TvShowViewMode = ViewMode.Poster,
            MovieViewMode = ViewMode.Poster,
            MusicArtistViewMode = ViewMode.Poster,
            PhotoAlbumViewMode = ViewMode.Poster,
            OtherVideosViewMode = ViewMode.Poster,
            AllOverviewViewMode = PlexMediaType.Movie,
        };

    public required ViewMode TvShowViewMode
    {
        get;
        set => SetProperty(ref field, value);
    } = ViewMode.Poster;

    public required ViewMode MovieViewMode
    {
        get;
        set => SetProperty(ref field, value);
    } = ViewMode.Poster;

    public ViewMode MusicArtistViewMode
    {
        get;
        set => SetProperty(ref field, value);
    } = ViewMode.Poster;

    public ViewMode PhotoAlbumViewMode
    {
        get;
        set => SetProperty(ref field, value);
    } = ViewMode.Poster;

    public ViewMode OtherVideosViewMode
    {
        get;
        set => SetProperty(ref field, value);
    } = ViewMode.Poster;

    public required PlexMediaType AllOverviewViewMode
    {
        get;
        set => SetProperty(ref field, value);
    } = PlexMediaType.Movie;
}
