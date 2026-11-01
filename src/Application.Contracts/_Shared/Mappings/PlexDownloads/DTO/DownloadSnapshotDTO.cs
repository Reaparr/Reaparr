namespace Reaparr.Application.Contracts;

public record DownloadSnapshotDTO
{
    public required List<ServerDownloadProgressDTO> Downloads { get; init; } = [];

    public required Dictionary<int, long> PatchSequenceByServer { get; init; } = new();
}
