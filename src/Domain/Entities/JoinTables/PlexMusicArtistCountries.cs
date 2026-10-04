namespace Reaparr.Domain;

public class PlexMusicArtistCountries
{
    public PlexMusicArtistCountries() { }

    public PlexMusicArtistCountries(int countryId, int plexLibraryId, int plexMusicArtistId)
    {
        CountryId = countryId;
        PlexMusicArtistId = plexMusicArtistId;
        PlexLibraryId = plexLibraryId;
    }

    [Column(Order = 1)]
    public int CountryId { get; init; }

    [Column(Order = 2)]
    public int PlexLibraryId { get; init; }

    [Column(Order = 3)]
    public int PlexMusicArtistId { get; init; }
}
