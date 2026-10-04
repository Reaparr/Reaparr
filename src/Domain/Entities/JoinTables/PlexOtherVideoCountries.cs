namespace Reaparr.Domain;

public class PlexOtherVideoCountries
{
    public PlexOtherVideoCountries() { }

    public PlexOtherVideoCountries(int countryId, int plexLibraryId, int plexOtherVideoId)
    {
        CountryId = countryId;
        PlexOtherVideoId = plexOtherVideoId;
        PlexLibraryId = plexLibraryId;
    }

    [Column(Order = 1)]
    public int CountryId { get; set; }

    [Column(Order = 2)]
    public int PlexLibraryId { get; set; }

    [Column(Order = 3)]
    public int PlexOtherVideoId { get; set; }
}
