namespace Reaparr.Domain;

public class PlexOtherVideoGenres
{
    public PlexOtherVideoGenres() { }

    public PlexOtherVideoGenres(int genresId, int plexLibraryId, int plexOtherVideoId)
    {
        GenresId = genresId;
        PlexOtherVideoId = plexOtherVideoId;
        PlexLibraryId = plexLibraryId;
    }

    [Column(Order = 1)]
    public int GenresId { get; set; }

    [Column(Order = 2)]
    public int PlexLibraryId { get; set; }

    [Column(Order = 3)]
    public int PlexOtherVideoId { get; set; }
}
