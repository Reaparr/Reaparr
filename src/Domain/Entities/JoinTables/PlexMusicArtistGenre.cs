namespace Reaparr.Domain;

public class PlexMusicArtistGenres
{
    public PlexMusicArtistGenres() { }

    public PlexMusicArtistGenres(int genresId, int plexLibraryId, int plexMusicArtistId)
    {
        GenresId = genresId;
        PlexMusicArtistId = plexMusicArtistId;
        PlexLibraryId = plexLibraryId;
    }

    [Column(Order = 1)]
    public int GenresId { get; set; }

    [Column(Order = 2)]
    public int PlexLibraryId { get; set; }

    [Column(Order = 3)]
    public int PlexMusicArtistId { get; set; }
}
