namespace Reaparr.Domain;

public record DirectDownloadSnapshot
{
    public required double SaveProgress { get; init; }
    public required int Status { get; init; }
    public required List<string> Urls { get; init; } = [];
    public required long TotalFileSize { get; init; }
    public required string FileName { get; init; } = string.Empty;
    public required string DownloadingFileExtension { get; init; } = string.Empty;
    public required List<DirectDownloadSnapshotChunk> Chunks { get; init; } = [];
    public string? ETag { get; init; }
    public string? LastModified { get; init; }
    public required bool IsSupportDownloadInRange { get; init; }
}

public record DirectDownloadSnapshotChunk
{
    public required string Id { get; init; } = string.Empty;
    public required long Start { get; init; }
    public required long End { get; init; }
    public required long Position { get; init; }
    public required int MaxTryAgainOnFailure { get; init; }
    public required int Timeout { get; init; }
}
