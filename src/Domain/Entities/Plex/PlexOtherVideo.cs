namespace Reaparr.Domain;

public class PlexOtherVideo : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.OtherVideos;

    public ICollection<PlexOtherVideoMediaData> MediaDataList { get; init; } = [];

    public ICollection<PlexActor> Actors { get; init; } = [];

    public ICollection<PlexGenre> Genres { get; init; } = [];

    public ICollection<PlexCountry> Countries { get; init; } = [];
}
