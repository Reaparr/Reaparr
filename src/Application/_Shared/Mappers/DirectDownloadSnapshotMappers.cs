using Downloader;

namespace Reaparr.Application;

public static class DirectDownloadSnapshotMapper
{
    public static DirectDownloadSnapshot ToSnapshot(this DownloadPackage package) =>
        new()
        {
            SaveProgress = package.SaveProgress,
            Status = (int)package.Status,
            Urls = package.Urls?.ToList() ?? [],
            TotalFileSize = package.TotalFileSize,
            FileName = package.FileName ?? string.Empty,
            DownloadingFileExtension = package.DownloadingFileExtension,
            Chunks = package
                .Chunks?.Select(x => new DirectDownloadSnapshotChunk
                {
                    Id = x.Id,
                    Start = x.Start,
                    End = x.End,
                    Position = x.Position,
                    MaxTryAgainOnFailure = x.MaxTryAgainOnFailure,
                    Timeout = x.Timeout,
                })
                .ToList()
                ?? [],
            ETag = package.ETag,
            LastModified = package.LastModified,
            IsSupportDownloadInRange = package.IsSupportDownloadInRange,
        };

    public static DownloadPackage ToDownloadPackage(this DirectDownloadSnapshot snapshot) =>
        new()
        {
            SaveProgress = snapshot.SaveProgress,
            Status = (Downloader.DownloadStatus)snapshot.Status,
            Urls = snapshot.Urls.ToArray(),
            TotalFileSize = snapshot.TotalFileSize,
            FileName = snapshot.FileName,
            DownloadingFileExtension = snapshot.DownloadingFileExtension,
            ETag = snapshot.ETag,
            LastModified = snapshot.LastModified,
            IsSupportDownloadInRange = snapshot.IsSupportDownloadInRange,
            Chunks = snapshot
                .Chunks.Select(x => new Chunk(x.Start, x.End)
                {
                    Id = x.Id,
                    Position = x.Position,
                    MaxTryAgainOnFailure = x.MaxTryAgainOnFailure,
                    Timeout = x.Timeout,
                })
                .ToArray(),
        };
}
