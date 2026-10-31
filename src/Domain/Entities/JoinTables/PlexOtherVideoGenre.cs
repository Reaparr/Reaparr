namespace Reaparr.Domain;

public class PlexOtherVideoGenres
{
    public PlexOtherVideoGenres() { }

    public PlexOtherVideoGenres(int genresId, int plexOtherVideoId)
    {
        GenresId = genresId;
        PlexOtherVideoId = plexOtherVideoId;
    }

    [Column(Order = 1)]
    public int GenresId { get; set; }

    [Column(Order = 2)]
    public int PlexOtherVideoId { get; set; }
}
