using System.Globalization;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Hooks;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.PlexApi;

internal static class PlexLibraryMediaApi
{
    public static async Task<Result<int>> GetLibraryMediaTotalCountAsync(
        this IPlexAPI client,
        long libraryKey,
        PlexMediaType mediaType,
        CancellationToken cancellationToken
    )
    {
        var response = await client
            .Content.ListContentAsync(
                CreateListContentRequest(libraryKey, mediaType, 0, 0, includeStreams: false, includeMetadata: false)
            )
            .ToResponse(cancellationToken);
        if (response.IsFailed)
            return response.ToResult().LogIfFailed();

        var rawValue = response.Value.MediaContainerWithMetadata?.MediaContainer?.TotalSize ?? 0;
        var safeValue = (int)Math.Max(0, Math.Min(rawValue, int.MaxValue));
        return Result.Ok(safeValue);
    }

    public static async Task<Result<PlexLibraryMediaPage>> GetMetadataForLibraryAsync(
        this IPlexAPI client,
        long libraryKey,
        PlexMediaType mediaType,
        int startIndex,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var response = await client
            .Content.ListContentAsync(
                CreateListContentRequest(libraryKey, mediaType, startIndex, pageSize, includeStreams: true)
            )
            .ToResponse(cancellationToken);
        if (response.IsFailed)
            return response.ToResult().LogIfFailed();

        var mediaContainer = response.Value.MediaContainerWithMetadata?.MediaContainer;
        var metadata = mediaContainer?.Metadata ?? [];
        int? total = mediaContainer?.TotalSize is { } rawTotal
            ? (int)Math.Max(0, Math.Min(rawTotal, int.MaxValue))
            : null;
        return Result.Ok(new PlexLibraryMediaPage(metadata.Select(x => x.ToMediaItemDTO()).ToList(), total));
    }

    public static Task<Result<List<PlexLibraryMediaItem>>> GetPhotoLibraryItemsAsync(
        this IPlexAPI client,
        long libraryKey,
        PlexMediaType mediaType,
        CancellationToken cancellationToken
    ) =>
        ReadPhotoPagesAsync(
            offset =>
                client
                    .Content.ListContentAsync(
                        CreateListContentRequest(
                            libraryKey,
                            mediaType,
                            offset,
                            100,
                            includeStreams: false,
                            requestMediaTypeOverride: mediaType == PlexMediaType.OtherVideos ? MediaType.Clip : null
                        )
                    )
                    .ToResponse(cancellationToken),
            response => response.MediaContainerWithMetadata?.MediaContainer,
            mediaType,
            "photo media"
        );

    public static async Task<Result<List<PlexLibraryMediaItem>>> GetPhotoFolderItemsAsync(
        this IPlexAPI client,
        long libraryKey,
        string? parent,
        IReadOnlyList<KeyValuePair<string, string>> additionalQuery,
        CancellationToken cancellationToken
    )
    {
        if (additionalQuery.Count > 0 && client is not PlexAPI)
            return Result
                .Fail<List<PlexLibraryMediaItem>>(
                    "The Plex SDK client cannot preserve returned photo folder query arguments"
                )
                .LogError();

        var readPage = new Func<int, Task<Result<GetSectionFolderResponse>>>(offset =>
            client
                .Library.GetSectionFolderAsync(
                    new GetSectionFolderRequest
                    {
                        SectionId = libraryKey,
                        Parent = parent,
                        XPlexContainerStart = offset,
                        XPlexContainerSize = 100,
                        IncludeMeta = BoolInt.True,
                    }
                )
                .ToResponse(cancellationToken)
        );

        if (additionalQuery.Count == 0)
            return await ReadPhotoPagesAsync(
                readPage,
                response => response.MediaContainerWithMetadata?.MediaContainer,
                PlexMediaType.PhotoImage,
                "photo folder"
            );

        var sdk = (PlexAPI)client;
        var hook = new AddQueryParametersHook(
            "getSectionFolder",
            $"/library/sections/{libraryKey}/folder",
            additionalQuery
        );
        sdk.SDKConfiguration.Hooks.RegisterBeforeRequestHook(hook);
        try
        {
            return await ReadPhotoPagesAsync(
                readPage,
                response => response.MediaContainerWithMetadata?.MediaContainer,
                PlexMediaType.PhotoImage,
                "photo folder"
            );
        }
        finally
        {
            sdk.SDKConfiguration.Hooks.beforeRequestHooks.Remove(hook);
        }
    }

    public static async Task<Result<List<PlexLibraryMediaItem>>> GetPhotoAlbumChildrenAsync(
        this IPlexAPI client,
        int albumKey,
        IReadOnlyList<KeyValuePair<string, string>> additionalQuery,
        CancellationToken cancellationToken
    )
    {
        if (additionalQuery.Count > 0 && client is not PlexAPI)
            return Result
                .Fail<List<PlexLibraryMediaItem>>(
                    "The Plex SDK client cannot preserve returned photo album query arguments"
                )
                .LogError();

        var readPage = new Func<int, Task<Result<GetMetadataChildrenResponse>>>(offset =>
            client
                .Library.GetMetadataChildrenAsync(
                    new GetMetadataChildrenRequest
                    {
                        Id = albumKey,
                        XPlexContainerStart = offset,
                        XPlexContainerSize = 100,
                        IncludeMeta = BoolInt.True,
                    }
                )
                .ToResponse(cancellationToken)
        );

        if (additionalQuery.Count == 0)
            return await ReadPhotoPagesAsync(
                readPage,
                response => response.MediaContainerWithMetadata?.MediaContainer,
                PlexMediaType.PhotoImage,
                "photo album children"
            );

        var sdk = (PlexAPI)client;
        var hook = new AddQueryParametersHook(
            "getMetadataChildren",
            $"/library/metadata/{albumKey}/children",
            additionalQuery
        );
        sdk.SDKConfiguration.Hooks.RegisterBeforeRequestHook(hook);
        try
        {
            return await ReadPhotoPagesAsync(
                readPage,
                response => response.MediaContainerWithMetadata?.MediaContainer,
                PlexMediaType.PhotoImage,
                "photo album children"
            );
        }
        finally
        {
            sdk.SDKConfiguration.Hooks.beforeRequestHooks.Remove(hook);
        }
    }

    private sealed class AddQueryParametersHook(
        string operationId,
        string expectedPath,
        IReadOnlyList<KeyValuePair<string, string>> additionalQuery
    ) : IBeforeRequestHook
    {
        public Task<HttpRequestMessage> BeforeRequestAsync(BeforeRequestContext context, HttpRequestMessage request)
        {
            if (
                !string.Equals(context.OperationID, operationId, StringComparison.Ordinal)
                || request.RequestUri is null
                || !string.Equals(request.RequestUri.AbsolutePath, expectedPath, StringComparison.Ordinal)
            )
                return Task.FromResult(request);

            var uriBuilder = new UriBuilder(request.RequestUri);
            var returnedNames = additionalQuery.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var parameters = ParseQueryParameters(uriBuilder.Query.TrimStart('?'))
                .Where(x => !returnedNames.Contains(x.Key))
                .Concat(additionalQuery);
            uriBuilder.Query = string.Join(
                "&",
                parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
            );
            request.RequestUri = uriBuilder.Uri;

            return Task.FromResult(request);
        }

        private static List<KeyValuePair<string, string>> ParseQueryParameters(string query)
        {
            var parameters = new List<KeyValuePair<string, string>>();
            foreach (var rawParameter in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = rawParameter.Split('=', 2);
                try
                {
                    parameters.Add(
                        new KeyValuePair<string, string>(
                            Uri.UnescapeDataString(parts[0]),
                            parts.Length == 1 ? string.Empty : Uri.UnescapeDataString(parts[1])
                        )
                    );
                }
                catch (UriFormatException)
                {
                    // SDK-generated requests are valid; leave malformed external values untouched.
                }
            }

            return parameters;
        }
    }

    private static ListContentRequest CreateListContentRequest(
        long libraryKey,
        PlexMediaType mediaType,
        int startIndex,
        int pageSize,
        bool includeStreams,
        bool includeMetadata = true,
        MediaType? requestMediaTypeOverride = null
    )
    {
        var requestMediaType = requestMediaTypeOverride ?? mediaType.ToMediaType();
        var addStreams =
            includeStreams
            && pageSize > 0
            && mediaType
                is PlexMediaType.Movie
                    or PlexMediaType.Episode
                    or PlexMediaType.MusicTrack
                    or PlexMediaType.OtherVideos;
        return new ListContentRequest
        {
            MediaQuery = new MediaQuery { Type = requestMediaType, Sort = "titleSort:asc" },
            XPlexContainerStart = startIndex,
            XPlexContainerSize = pageSize,
            SectionId = libraryKey,
            Unwatched = null,
            IncludeGuids = includeMetadata ? BoolInt.True : null,
            IncludeMeta = includeMetadata ? BoolInt.True : null,
            IncludeOptionalElements = addStreams ? "Stream" : null,
            PmsApiVersion = addStreams ? "1.0" : null,
        };
    }

    private static async Task<Result<List<PlexLibraryMediaItem>>> ReadPhotoPagesAsync<T>(
        Func<int, Task<Result<T>>> readPage,
        Func<T, MediaContainerWithMetadataMediaContainer?> getContainer,
        PlexMediaType missingType,
        string description
    )
    {
        var items = new List<PlexLibraryMediaItem>();
        var offset = 0;
        var pages = new PhotoPageState();
        while (true)
        {
            var response = await readPage(offset);
            if (response.IsCancelledOrFailed)
                return response.ToResult<List<PlexLibraryMediaItem>>().LogIfFailed();

            var container = getContainer(response.Value);
            if (container is null)
                return Result
                    .Fail<List<PlexLibraryMediaItem>>($"Plex returned no typed {description} container")
                    .LogError();

            var page = pages.Validate(container, offset, missingType, description);
            if (page.IsFailed)
                return page.ToResult<List<PlexLibraryMediaItem>>();

            items.AddRange(page.Value.Items);
            if (page.Value.Complete)
                return Result.Ok(items);
            offset = page.Value.NextOffset;
        }
    }

    private static IEnumerable<PlexLibraryMediaItem> Enumerate(
        MediaContainerWithMetadataMediaContainer container,
        PlexMediaType missingType
    )
    {
        foreach (var item in container.Directory ?? [])
            yield return NormalizePhotoItem(item, PlexLibraryMediaItemOrigin.Directory, missingType);
        foreach (var item in container.Metadata ?? [])
            yield return NormalizePhotoItem(item, PlexLibraryMediaItemOrigin.Metadata, missingType);
        foreach (var item in container.Photo ?? [])
            yield return NormalizePhotoItem(item, PlexLibraryMediaItemOrigin.Photo, missingType);
        foreach (var item in container.Video ?? [])
            yield return NormalizePhotoItem(item, PlexLibraryMediaItemOrigin.Video, missingType);
    }

    private static PlexLibraryMediaItem NormalizePhotoItem(
        Metadata metadata,
        PlexLibraryMediaItemOrigin origin,
        PlexMediaType missingType
    )
    {
        var type = metadata.Type?.Trim().ToLowerInvariant();
        var mediaType = type switch
        {
            null or "" when string.Equals(metadata.Subtype?.Trim(), "photo", StringComparison.OrdinalIgnoreCase) =>
                PlexMediaType.PhotoImage,
            null or "" when origin == PlexLibraryMediaItemOrigin.Photo => PlexMediaType.PhotoImage,
            null or "" when origin == PlexLibraryMediaItemOrigin.Video => PlexMediaType.OtherVideos,
            null or "" when origin == PlexLibraryMediaItemOrigin.Directory => (PlexMediaType?)null,
            null or "" => missingType,
            "photo" or "image" => PlexMediaType.PhotoImage,
            "clip" or "video" or "movie" => PlexMediaType.OtherVideos,
            "photoalbum" or "album" => PlexMediaType.PhotoAlbum,
            _ => null,
        };
        PlexLibraryMediaItemShape shape;
        if (metadata.Media is { Count: > 0 })
            shape = PlexLibraryMediaItemShape.Asset;
        else if (
            IsPositiveRatingKey(metadata.RatingKey)
            && !string.IsNullOrWhiteSpace(metadata.Key)
            && type is null or "" or "photo" or "image" or "photoalbum" or "album" or "directory"
        )
            shape = PlexLibraryMediaItemShape.Album;
        else if (
            string.IsNullOrWhiteSpace(metadata.RatingKey)
            && !string.IsNullOrWhiteSpace(metadata.Key)
            && (origin == PlexLibraryMediaItemOrigin.Directory || type is null or "" or "folder" or "directory")
        )
            shape = PlexLibraryMediaItemShape.Folder;
        else
            shape = PlexLibraryMediaItemShape.Unknown;
        return new PlexLibraryMediaItem(metadata, origin, mediaType, shape);
    }

    private static bool IsPositiveRatingKey(string? value) => int.TryParse(value, out var parsed) && parsed > 0;

    internal enum PlexLibraryMediaItemOrigin
    {
        Directory,
        Metadata,
        Photo,
        Video,
    }

    internal enum PlexLibraryMediaItemShape
    {
        Unknown,
        Folder,
        Album,
        Asset,
    }

    internal sealed record PlexLibraryMediaItem(
        Metadata Metadata,
        PlexLibraryMediaItemOrigin Origin,
        PlexMediaType? MediaType,
        PlexLibraryMediaItemShape Shape
    );

    private sealed class PhotoPageState
    {
        private long? _total;
        private readonly HashSet<string> _windows = [];

        public Result<PhotoPageResult> Validate(
            MediaContainerWithMetadataMediaContainer container,
            int requestedOffset,
            PlexMediaType missingType,
            string description
        )
        {
            var items = Enumerate(container, missingType).ToList();
            if (container.Offset is < 0 || container.Size is < 0 || container.TotalSize is < 0)
                return Result
                    .Fail<PhotoPageResult>($"Plex returned invalid {description} pagination values")
                    .LogError();

            var isUnpaged = container.Offset is null && container.TotalSize is null;
            var offset = container.Offset ?? requestedOffset;
            if (offset != requestedOffset)
                return Result
                    .Fail<PhotoPageResult>(
                        $"Plex returned {description} offset {offset} for requested offset {requestedOffset}"
                    )
                    .LogError();
            if (container.Size is { } size && size != items.Count)
                return Result
                    .Fail<PhotoPageResult>($"Plex returned a {description} size inconsistent with its item window")
                    .LogError();
            if (_total.HasValue && container.TotalSize is null)
                return Result
                    .Fail<PhotoPageResult>($"Plex omitted the {description} total during pagination")
                    .LogError();
            if (_total.HasValue && container.TotalSize != _total)
                return Result
                    .Fail<PhotoPageResult>($"Plex changed the reported {description} total during enumeration")
                    .LogError();
            if (items.Count > 0 && !_windows.Add(GetWindowIdentity(items)))
                return Result
                    .Fail<PhotoPageResult>($"Plex returned a repeated nonempty {description} window")
                    .LogError();

            _total ??= container.TotalSize;

            var total = _total;
            var end = (long)requestedOffset + items.Count;
            if (total is { } knownTotal && (requestedOffset > knownTotal || end > knownTotal))
                return Result
                    .Fail<PhotoPageResult>($"Plex returned a {description} window beyond its reported total")
                    .LogError();
            if (isUnpaged && requestedOffset > 0)
                return Result
                    .Fail<PhotoPageResult>($"Plex returned unpaged {description} metadata after pagination started")
                    .LogError();
            if (isUnpaged)
                return Result.Ok(new PhotoPageResult(items, true, requestedOffset));
            if (items.Count == 0)
            {
                if (total is { } knownEmptyTotal && requestedOffset != knownEmptyTotal)
                    return Result.Fail<PhotoPageResult>($"Plex returned an incomplete {description} page").LogError();
                return Result.Ok(new PhotoPageResult(items, true, requestedOffset));
            }
            if (total is { } knownTotalAtEnd && end == knownTotalAtEnd)
                return Result.Ok(new PhotoPageResult(items, true, requestedOffset));
            if (end > int.MaxValue)
                return Result.Fail<PhotoPageResult>($"Plex returned an unadvanceable {description} page").LogError();
            return Result.Ok(new PhotoPageResult(items, false, (int)end));
        }

        private static string GetWindowIdentity(IReadOnlyList<PlexLibraryMediaItem> items) =>
            string.Join("\u001f", items.Select(GetWindowItemIdentity).OrderBy(x => x, StringComparer.Ordinal));

        private static string GetWindowItemIdentity(PlexLibraryMediaItem item)
        {
            var media = item.Metadata.Media?.FirstOrDefault();
            var part = media?.Part?.FirstOrDefault();
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{item.Origin}\u001e{item.Metadata.RatingKey}\u001e{item.Metadata.Key}\u001e{item.Metadata.Type}\u001e{item.Metadata.Subtype}\u001e{item.Metadata.Media?.Count}\u001e{media?.Id}\u001e{part?.Id}\u001e{part?.Key}"
            );
        }
    }

    private readonly record struct PhotoPageResult(
        IReadOnlyList<PlexLibraryMediaItem> Items,
        bool Complete,
        int NextOffset
    );
}

internal sealed record PlexLibraryMediaPage(IReadOnlyList<LibraryMediaItemDTO> Items, int? Total);
