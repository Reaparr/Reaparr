namespace Reaparr.Domain;

public class PlexMusicArtistGenres
{
    public PlexMusicArtistGenres() { }

    public PlexMusicArtistGenres(int genresId, int plexMusicArtistId)
    {
        GenresId = genresId;
        PlexMusicArtistId = plexMusicArtistId;
    }

    [Column(Order = 1)]
    public int GenresId { get; set; }

    [Column(Order = 2)]
    public int PlexMusicArtistId { get; set; }
}
