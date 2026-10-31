namespace Reaparr.Domain;

public class PlexMusicArtistCountries
{
    public PlexMusicArtistCountries() { }

    public PlexMusicArtistCountries(int countryId, int plexMusicArtistId)
    {
        CountryId = countryId;
        PlexMusicArtistId = plexMusicArtistId;
    }

    [Column(Order = 1)]
    public int CountryId { get; init; }

    [Column(Order = 2)]
    public int PlexMusicArtistId { get; init; }
}
