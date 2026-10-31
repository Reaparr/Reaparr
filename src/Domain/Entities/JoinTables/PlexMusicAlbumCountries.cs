namespace Reaparr.Domain;

public class PlexMusicAlbumCountries
{
    public PlexMusicAlbumCountries() { }

    public PlexMusicAlbumCountries(int countryId, int plexMusicAlbumId)
    {
        CountryId = countryId;
        PlexMusicAlbumId = plexMusicAlbumId;
    }

    /// <summary>
    /// Gets the identifier of the country catalogue entry.
    /// </summary>
    [Column(Order = 1)]
    public int CountryId { get; init; }

    /// <summary>
    /// Gets the identifier of the Plex music album.
    /// </summary>
    [Column(Order = 2)]
    public int PlexMusicAlbumId { get; init; }
}
