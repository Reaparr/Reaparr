namespace Reaparr.Domain;

public class PlexOtherVideoMediaData : BasePlexMediaData
{
    public required int PlexOtherVideoId { get; set; }

    public PlexOtherVideo? PlexOtherVideo { get; set; }

    public required int PartIndex { get; set; }

    public string? OriginalFilePath { get; set; }

    public string? SourceRelativePath { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? VideoProfile { get; set; }

    public decimal? VideoFrameRate { get; set; }

    public override PlexMediaType Type => PlexMediaType.OtherVideos;
}
