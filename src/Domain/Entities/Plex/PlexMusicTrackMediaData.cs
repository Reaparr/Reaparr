namespace Reaparr.Domain;

public class PlexMusicTrackMediaData : BasePlexMediaData
{
    public override PlexMediaType Type => PlexMediaType.Song;

    public required int PlexTrackId { get; set; }

    public PlexMusicTrack? PlexTrack { get; set; }

    public required int PartIndex { get; set; }

    public string? OriginalFilePath { get; set; }

    public string? SourceRelativePath { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public string? VideoProfile { get; set; }

    public decimal? VideoFrameRate { get; set; }
}
