namespace Reaparr.Domain;

public class PlexOtherVideo : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.OtherVideos;

    [Column(Order = 3)]
    public required int Year { get; set; }

    [Column(Order = 16)]
    public required DateTime? OriginallyAvailableAt { get; init; }

    [Column(Order = 10)]
    public required string Summary { get; init; } = string.Empty;

    [Column(Order = 23)]
    public required string Guid { get; init; } = string.Empty;

    public ICollection<PlexOtherVideoMediaData> MediaDataList { get; init; } = [];

    public ICollection<PlexActor> Actors { get; init; } = [];

    public ICollection<PlexGenreVideo> Genres { get; init; } = [];

    public ICollection<PlexCountry> Countries { get; init; } = [];
}
