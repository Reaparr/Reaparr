namespace Reaparr.Application.Contracts;

public record PlexQualityDTO
{
    public int Id =>
        Quality switch
        {
            VideoQuality.None => 0,
            VideoQuality.Unknown => -1,
            VideoQuality.SubSD_144p => 1,
            VideoQuality.SubSD_CIF => 2,
            VideoQuality.nHD => 3,
            VideoQuality.SD => 4,
            VideoQuality.DVD => 5,
            VideoQuality.HD => 6,
            VideoQuality.FullHD => 7,
            VideoQuality.QHD => 8,
            VideoQuality.UHD_4K => 9,
            VideoQuality.UHD_8K => 10,
            _ => throw new InvalidOperationException($"Unsupported video quality: {Quality}."),
        };

    public required string Name { get; init; }
    public required int Count { get; init; }

    public required VideoQuality Quality { get; init; }
}
