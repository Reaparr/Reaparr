namespace Reaparr.Data.Contracts;

public static partial class DbSetExtensions
{
    public static IQueryable<DownloadTaskPhotoAlbum> IncludeAll(this IQueryable<DownloadTaskPhotoAlbum> tasks) =>
        tasks
            .Include(x => x.PlexServer)
            .Include(x => x.PlexLibrary)
            .Include(x => x.Children.OrderBy(y => y.CreatedAt))
                .ThenInclude(x => x.PlexServer)
            .Include(x => x.Children.OrderBy(y => y.CreatedAt))
                .ThenInclude(x => x.PlexLibrary)
            .Include(x => x.Children)
                .ThenInclude(x => x.Children.OrderBy(y => y.CreatedAt))
                    .ThenInclude(x => x.PlexServer)
            .Include(x => x.Children)
                .ThenInclude(x => x.Children.OrderBy(y => y.CreatedAt))
                    .ThenInclude(x => x.PlexLibrary);

    public static IQueryable<DownloadTaskPhotoImage> IncludeAll(this IQueryable<DownloadTaskPhotoImage> tasks) =>
        tasks
            .Include(x => x.PlexServer)
            .Include(x => x.PlexLibrary)
            .Include(x => x.Children.OrderBy(y => y.CreatedAt))
                .ThenInclude(x => x.PlexServer)
            .Include(x => x.Children.OrderBy(y => y.CreatedAt))
                .ThenInclude(x => x.PlexLibrary);
}
