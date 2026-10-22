namespace Reaparr.Domain;

public class PlexOtherVideoCountries
{
    public PlexOtherVideoCountries() { }

    public PlexOtherVideoCountries(int countryId, int plexOtherVideoId)
    {
        CountryId = countryId;
        PlexOtherVideoId = plexOtherVideoId;
    }


    [Column(Order = 1)]
    public int CountryId { get; set; }

    [Column(Order = 2)]
    public int PlexOtherVideoId { get; set; }

}
