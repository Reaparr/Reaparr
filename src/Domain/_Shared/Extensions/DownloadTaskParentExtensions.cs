namespace Reaparr.Domain;

public static class DownloadTaskParentExtensions
{
    public static DownloadTaskDirectory GetDirectoryMeta(this DownloadTaskParentBase parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var directoryMeta = FindDirectoryMeta(parent);
        return directoryMeta
            ?? throw new InvalidOperationException(
                $"Download task {parent.Id} does not contain a descendant file with directory metadata."
            );
    }

    private static DownloadTaskDirectory? FindDirectoryMeta(DownloadTaskParentBase parent)
    {
        foreach (var child in GetChildren(parent))
        {
            switch (child)
            {
                case DownloadTaskFileBase file:
                    return file.DirectoryMeta;
                case DownloadTaskParentBase nestedParent:
                {
                    var directoryMeta = FindDirectoryMeta(nestedParent);
                    if (directoryMeta is not null)
                        return directoryMeta;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(parent),
                        child.GetType(),
                        "Unsupported download task child."
                    );
            }
        }

        return null;
    }

    private static IEnumerable<DownloadTaskBase> GetChildren(DownloadTaskParentBase parent) =>
        parent switch
        {
            DownloadTaskMovie movie => movie.Children,
            DownloadTaskTvShow tvShow => tvShow.Children,
            DownloadTaskTvShowSeason season => season.Children,
            DownloadTaskTvShowEpisode episode => episode.Children,
            DownloadTaskPhotoAlbum photoAlbum => photoAlbum.Children,
            DownloadTaskPhotoImage photoImage => photoImage.Children,
            DownloadTaskMusicArtist musicArtist => musicArtist.Children,
            DownloadTaskMusicAlbum musicAlbum => musicAlbum.Children,
            DownloadTaskMusicTrack musicTrack => musicTrack.Children,
            DownloadTaskOtherVideo otherVideo => otherVideo.Children,
            _ => throw new ArgumentOutOfRangeException(
                nameof(parent),
                parent.GetType(),
                "Unsupported download task parent."
            ),
        };
}
