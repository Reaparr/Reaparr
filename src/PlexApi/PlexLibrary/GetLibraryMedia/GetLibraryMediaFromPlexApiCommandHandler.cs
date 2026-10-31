using System.Globalization;
using LukeHagar.PlexAPI.SDK;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;
using PhotoItem = Reaparr.PlexApi.PlexLibraryMediaApi.PlexLibraryMediaItem;
using PhotoItemShape = Reaparr.PlexApi.PlexLibraryMediaApi.PlexLibraryMediaItemShape;

namespace Reaparr.PlexApi;

/// <summary>
/// Retrieves all media metadata from the PlexApi for a given <see cref="PlexLibrary"/> and returns it as a <see cref="LibraryMetadata"/>.
/// This service is an extra layer of abstraction to convert incoming DTO's from the PlexAPI to workable entities.
/// This was done in order to keep all PlexApi related DTO's in the infrastructure layer.
/// </summary>
public class GetLibraryMediaFromPlexApiCommandHandler
    : ICommandHandler<GetLibraryMediaFromPlexApiCommand, Result<LibraryMetadata>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetLibraryMediaFromPlexApiCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        ILibrarySyncProgressStore librarySyncProgressStore,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetLibraryMediaFromPlexApiCommandHandler>();
        _commandExecutor = commandExecutor;
        _librarySyncProgressStore = librarySyncProgressStore;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<LibraryMetadata>> ExecuteAsync(
        GetLibraryMediaFromPlexApiCommand command,
        CancellationToken ct
    )
    {
        var plexLibrary = command.PlexLibrary;
        var result = await Result.Try(async Task<Result<LibraryMetadata>> () =>
        {
            var library = plexLibrary;
            if (command.MediaType is null)
            {
                // Retrieve an updated version of the PlexLibrary
                var sectionsResult = await _commandExecutor.Send(
                    new GetLibrarySectionsCommand(plexLibrary.PlexServerId),
                    ct
                );
                if (sectionsResult.IsFailed)
                {
                    await _librarySyncProgressStore.StartAsync(plexLibrary.Id, plexLibrary.Type);
                    return sectionsResult.ToResult();
                }

                library = sectionsResult.Value.Find(x => x.Key == plexLibrary.Key);
                if (library is null)
                {
                    await _librarySyncProgressStore.StartAsync(plexLibrary.Id, plexLibrary.Type);
                    return ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibrary.Id);
                }

                library.Id = plexLibrary.Id;
                library.PlexServerId = plexLibrary.PlexServerId;
                library.DefaultDestinationId = library.Type.ToDefaultDestinationFolderId();

                var totalsResult = await _commandExecutor.Send(new GetLibraryMediaTotalsCommand(library), ct);
                if (totalsResult.IsCancelled)
                {
                    await _librarySyncProgressStore.StartAsync(plexLibrary.Id, plexLibrary.Type);
                    return totalsResult.ToResult();
                }

                var progressTotals = totalsResult.IsSuccess ? totalsResult.Value : new Dictionary<PlexMediaType, int>();
                await _librarySyncProgressStore.StartAsync(plexLibrary.Id, plexLibrary.Type, progressTotals);
            }

            var mediaType = command.MediaType ?? library.Type;
            if (command.MediaType is null && mediaType == PlexMediaType.PhotoAlbum)
            {
                var clientResult = await _plexApiClientFactory.CreateClientAsync(library.PlexServerId, 30, 3, null);
                if (clientResult.IsFailed)
                    return clientResult.ToResult().LogIfFailed();

                var populateResult = await PopulatePhotoLibraryAsync(clientResult.Value, library, _log, ct);
                if (populateResult.IsFailed)
                    return populateResult.LogIfFailed();

                return Result.Ok(
                    new LibraryMetadata(library)
                    {
                        Countries = [],
                        GenresVideo = [],
                        GenresAudio = [],
                        Actors = [],
                        PhotoClipCount = library.PhotoMediaData.Count(x => x.AssetKind == PhotoAssetKind.VideoClip),
                    }
                );
            }
            var mediaResult = await _commandExecutor.Send(
                new GetAllMediaByTypeFromPlexApiCommand(library, mediaType),
                ct
            );
            if (mediaResult.IsFailed)
                return mediaResult.ToResult().LogIfFailed();

            // Pre-sort the media list
            var media = mediaResult.Value.OrderByNatural(x => x.SortTitle).ToList();

            // Set Sort index based on OrderByNatural(x => x.SortTitle), per parent for descendants
            var sortIndices = new Dictionary<string, int>();
            foreach (var item in media)
            {
                var parentKey = command.MediaType is null ? string.Empty : item.ParentRatingKey;
                sortIndices.TryGetValue(parentKey, out var index);
                item.SortIndex = sortIndices[parentKey] = index + 1;
            }

            switch (mediaType)
            {
                case PlexMediaType.Movie:
                    library.Movies.AddRange(media.ToPlexMovies());
                    break;
                case PlexMediaType.TvShow:
                    library.TvShows.AddRange(media.ToPlexTvShows());
                    break;
                case PlexMediaType.MusicArtist:
                    library.Music.AddRange(media.ToPlexMusicArtists());
                    break;
                case PlexMediaType.MusicAlbum:
                    library.Albums.AddRange(media.ToPlexMusicAlbums());
                    break;
                case PlexMediaType.MusicTrack:
                    library.Tracks.AddRange(media.ToPlexMusicTracks());
                    break;
                case PlexMediaType.PhotoAlbum:
                    library.PhotoAlbums.AddRange(media.ToPlexPhotoAlbums());
                    break;
                case PlexMediaType.PhotoImage:
                    library.PhotoMediaData.AddRange(media.ToPlexPhotoMediaDataList());
                    break;
                case PlexMediaType.OtherVideos:
                    library.OtherVideos.AddRange(media.ToPlexOtherVideos());
                    break;
                default:
                    return Result.Fail("Type {PlexMediaType} is not supported for library retrieval", mediaType);
            }

            return Result.Ok(
                new LibraryMetadata(library)
                {
                    Countries = media.SelectMany(x => x.Country).ToPlexCountry(),
                    GenresVideo = !mediaType.IsMusicType() ? media.SelectMany(x => x.Genre).ToPlexGenreVideo() : [],
                    GenresAudio = mediaType.IsMusicType() ? media.SelectMany(x => x.Genre).ToPlexGenreAudio() : [],
                    Actors = media.SelectMany(x => x.Role).ToPlexActor(),
                    PhotoClipCount =
                        mediaType == PlexMediaType.PhotoImage
                            ? media.Count(x => x.Type is PlexMediaType.OtherVideos or PlexMediaType.Movie)
                            : 0,
                }
            );
        });

        result.LogIfFailed();

        if (result.IsFailed && command.MediaType is null)
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibrary.Id, result.ToResult());

        return result;
    }

    private static async Task<Result> PopulatePhotoLibraryAsync(
        IPlexAPI client,
        PlexLibrary library,
        ILogger log,
        CancellationToken ct
    )
    {
        var roots = library.PhotoFolders.Where(x => x.ParentId is null).ToList();
        var sectionIdResult = ParsePhotoSectionId(library.Key);
        if (sectionIdResult.IsFailed)
            return sectionIdResult.ToResult().LogIfFailed();

        var state = new PhotoCatalogState(sectionIdResult.Value);
        foreach (
            var mediaType in new[] { PlexMediaType.PhotoImage, PlexMediaType.PhotoAlbum, PlexMediaType.OtherVideos }
        )
        {
            var pageResult = await client.GetPhotoLibraryItemsAsync(sectionIdResult.Value, mediaType, ct);
            if (pageResult.IsCancelledOrFailed)
                return pageResult.ToResult().LogIfFailed();

            foreach (var item in pageResult.Value)
            {
                var itemResult = await ProcessPhotoItemAsync(client, library, item, state, ct);
                if (itemResult.IsCancelledOrFailed)
                    return itemResult.LogIfFailed();
            }
        }

        while (state.PendingFolderParents.Count > 0)
        {
            var pendingFolder = state.PendingFolderParents.First();
            state.PendingFolderParents.Remove(pendingFolder.Key);
            var folderResult = await ReadPhotoFolderTreeAsync(
                client,
                sectionIdResult.Value,
                pendingFolder.Value,
                state,
                ct
            );
            if (folderResult.IsCancelledOrFailed)
                return folderResult.LogIfFailed();
        }

        var duplicatePart = state.Assets.Values.GroupBy(x => x.Entity.PlexApiPartId).FirstOrDefault(x => x.Count() > 1);
        if (duplicatePart is not null)
            return Result.Fail($"Plex returned duplicate photo part identity {duplicatePart.Key}").LogError();

        foreach (var asset in state.Assets.Values)
        {
            asset.Entity.PlexLibraryId = library.Id;
            asset.Entity.PlexServerId = library.PlexServerId;
        }

        var folders = BuildPhotoFolders(roots, state.Assets.Values.Select(x => x.Entity).ToList(), library, log);
        foreach (var asset in state.Assets.Values)
        {
            foreach (var albumKey in asset.AlbumKeys)
            {
                var album = state.Albums[albumKey];
                var membership = new PlexPhotoAlbumMembership
                {
                    PlexPhotoAlbumId = 0,
                    PlexPhotoMediaDataId = 0,
                    PlexPhotoAlbum = album,
                    PlexPhotoMediaData = asset.Entity,
                };
                album.Memberships.Add(membership);
                asset.Entity.AlbumMemberships.Add(membership);
            }
        }

        foreach (var album in state.Albums.Values.OrderBy(x => x.PlexApiRatingKey))
            library.PhotoAlbums.Add(album);
        foreach (var asset in state.Assets.Values.OrderBy(x => x.Entity.PlexApiRatingKey))
            library.PhotoMediaData.Add(asset.Entity);
        foreach (var folder in folders)
            library.PhotoFolders.Add(folder);
        return Result.Ok();
    }

    private static async Task<Result> ProcessPhotoItemAsync(
        IPlexAPI client,
        PlexLibrary library,
        PhotoItem item,
        PhotoCatalogState state,
        CancellationToken ct
    )
    {
        var validationResult = ValidatePhotoItem(item, allowFolder: true, state.SectionId);
        if (validationResult.IsFailed)
            return validationResult.LogIfFailed();

        switch (item.Shape)
        {
            case PhotoItemShape.Folder:
                if (!TryGetPhotoFolderNavigation(item.Metadata.Key, state.SectionId, out var folderNavigation))
                    return Result.Fail("Plex returned a photo folder without a valid parent token").LogError();

                state.PendingFolderParents.TryAdd(folderNavigation.Identity, folderNavigation);
                return Result.Ok();
            case PhotoItemShape.Asset:
                return MergePhotoAsset(item, null, state.Assets);
            case PhotoItemShape.Album:
            {
                if (!TryGetPhotoAlbumNavigation(item.Metadata.Key, out var albumNavigation))
                    return Result.Fail("Plex returned a photo album without a valid children token").LogError();

                var albumId = ParsePhotoRatingKey(item.Metadata);
                var navigationKey = $"{albumId}\u001f{albumNavigation.Identity}";
                if (!state.AlbumNavigationKeys.Add(navigationKey))
                    return Result.Ok();

                if (!state.Albums.TryGetValue(albumId, out var album))
                {
                    album = item.Metadata.ToPlexPhotoAlbum(item.MediaType ?? PlexMediaType.PhotoAlbum);
                    album.PlexLibraryId = library.Id;
                    album.PlexServerId = library.PlexServerId;
                    state.Albums.Add(albumId, album);
                }

                var childrenResult = await client.GetPhotoAlbumChildrenAsync(
                    albumNavigation.RequestId,
                    albumNavigation.AdditionalQuery,
                    ct
                );
                if (childrenResult.IsCancelledOrFailed)
                    return childrenResult.ToResult().LogIfFailed();

                foreach (var child in childrenResult.Value)
                {
                    var childValidation = ValidatePhotoItem(child, allowFolder: false, state.SectionId);
                    if (childValidation.IsFailed)
                        return childValidation.LogIfFailed();
                    if (child.Shape != PhotoItemShape.Asset)
                        return Result.Fail("Plex returned a nested photo album or folder in album children").LogError();
                    var mergeResult = MergePhotoAsset(child, albumId, state.Assets);
                    if (mergeResult.IsFailed)
                        return mergeResult;
                }

                return Result.Ok();
            }
            default:
                return Result.Fail("Plex returned an unsupported photo item shape").LogError();
        }
    }

    private static async Task<Result> ReadPhotoFolderTreeAsync(
        IPlexAPI client,
        long sectionId,
        PhotoFolderNavigation navigation,
        PhotoCatalogState state,
        CancellationToken ct
    )
    {
        var identity = navigation.Identity;
        if (state.CompletedFolderParents.Contains(identity))
            return Result.Ok();
        if (!state.ActiveFolderParents.Add(identity))
            return Result.Fail($"Plex returned a cyclic photo folder navigation path at '{identity}'").LogError();

        var pageResult = await client.GetPhotoFolderItemsAsync(
            sectionId,
            navigation.Parent,
            navigation.AdditionalQuery,
            ct
        );
        if (pageResult.IsCancelledOrFailed)
            return pageResult.ToResult().LogIfFailed();

        foreach (var item in pageResult.Value)
        {
            var validationResult = ValidatePhotoItem(item, allowFolder: true, sectionId);
            if (validationResult.IsFailed)
                return validationResult.LogIfFailed();

            if (item.Shape == PhotoItemShape.Asset)
            {
                var mergeResult = MergePhotoAsset(item, null, state.Assets);
                if (mergeResult.IsFailed)
                    return mergeResult;
                continue;
            }

            if (item.Shape == PhotoItemShape.Album)
                return Result.Fail("Plex returned a nested photo album in folder traversal").LogError();
            if (!TryGetPhotoFolderNavigation(item.Metadata.Key, sectionId, out var childNavigation))
                return Result.Fail("Plex returned a photo folder without a valid parent token").LogError();

            var childResult = await ReadPhotoFolderTreeAsync(client, sectionId, childNavigation, state, ct);
            if (childResult.IsCancelledOrFailed)
                return childResult.LogIfFailed();
        }

        state.ActiveFolderParents.Remove(identity);
        state.CompletedFolderParents.Add(identity);
        return Result.Ok();
    }

    private static Result MergePhotoAsset(PhotoItem item, int? albumKey, Dictionary<int, PhotoAssetState> assets)
    {
        var ratingKey = ParsePhotoRatingKey(item.Metadata);
        if (!assets.TryGetValue(ratingKey, out var state))
        {
            state = new PhotoAssetState(item.Metadata.ToPlexPhotoMediaData(item.MediaType!.Value));
            assets.Add(ratingKey, state);
        }
        else if (!AreEquivalentPhotoAssets(state.Entity, item))
        {
            return Result
                .Fail($"Plex returned conflicting single-file representations for photo asset {ratingKey}")
                .LogError();
        }

        if (albumKey is > 0)
            state.AlbumKeys.Add(albumKey.Value);
        return Result.Ok();
    }

    private static Result ValidatePhotoItem(PhotoItem item, bool allowFolder, long sectionId)
    {
        var metadata = item.Metadata;
        if (metadata.LibrarySectionID is { } owner && owner != sectionId)
            return Result.Fail("Plex returned photo metadata belonging to another library").LogError();

        switch (item.Shape)
        {
            case PhotoItemShape.Folder:
                return !allowFolder || string.IsNullOrWhiteSpace(metadata.Key)
                    ? Result.Fail("Plex returned a photo navigation item without a key").LogError()
                    : Result.Ok();
            case PhotoItemShape.Album:
                return !IsPositivePhotoRatingKey(metadata.RatingKey) || string.IsNullOrWhiteSpace(metadata.Key)
                    ? Result.Fail("Plex returned metadata without an asset or album shape").LogError()
                    : Result.Ok();
            case PhotoItemShape.Asset:
            {
                var media = metadata.Media;
                if (!IsPositivePhotoRatingKey(metadata.RatingKey))
                    return Result.Fail("Plex returned a photo asset without a positive rating key").LogError();
                if (media is not { Count: 1 } || media[0].Part?.Count != 1)
                    return Result
                        .Fail("Plex photo assets must contain exactly one Media item with exactly one Part")
                        .LogError();

                var mediaItem = media[0];
                var part = mediaItem.Part![0];
                if (
                    Convert.ToInt32(mediaItem.Id) <= 0
                    || Convert.ToInt32(part.Id) <= 0
                    || string.IsNullOrWhiteSpace(part.Key)
                )
                    return Result
                        .Fail("Plex returned a photo asset without valid single-file transfer identity")
                        .LogError();
                if (IsPhotoChildrenKey(metadata.Key) || item.MediaType == PlexMediaType.PhotoAlbum)
                    return Result.Fail("Plex returned a photo album with asset parts").LogError();
                return item.MediaType is PlexMediaType.PhotoImage or PlexMediaType.OtherVideos
                    ? Result.Ok()
                    : Result.Fail("Plex returned an unknown photo asset type").LogError();
            }
            default:
                if (string.IsNullOrWhiteSpace(metadata.RatingKey))
                    return !allowFolder || string.IsNullOrWhiteSpace(metadata.Key)
                        ? Result.Fail("Plex returned a photo navigation item without a key").LogError()
                        : Result.Fail("Plex returned a navigation item with an unsupported type").LogError();
                return IsPositivePhotoRatingKey(metadata.RatingKey) && !string.IsNullOrWhiteSpace(metadata.Key)
                    ? Result.Fail("Plex returned an unsupported photo album type").LogError()
                    : Result.Fail("Plex returned metadata without an asset or album shape").LogError();
        }
    }

    private static bool AreEquivalentPhotoAssets(PlexPhotoMediaData existing, PhotoItem observation)
    {
        var media = observation.Metadata.Media![0];
        var part = media.Part![0];
        var sourcePath = string.IsNullOrWhiteSpace(part.File) ? null : part.File;
        var duration = (part.Duration, media.Duration) switch
        {
            (> 0, _) => part.Duration,
            (_, > 0) => media.Duration,
            _ => null,
        };
        var kind = observation.MediaType == PlexMediaType.OtherVideos ? PhotoAssetKind.VideoClip : PhotoAssetKind.Image;
        return existing.PlexApiMediaId == Convert.ToInt32(media.Id)
            && existing.PlexApiPartId == Convert.ToInt32(part.Id)
            && existing.AssetKind == kind
            && AreEquivalentPhotoSourcePaths(existing.SourcePath, sourcePath)
            && string.Equals(existing.Key, part.Key, StringComparison.Ordinal)
            && existing.Size == Math.Max(0, part.Size ?? 0)
            && existing.Duration == duration;
    }

    private static bool AreEquivalentPhotoSourcePaths(string? existing, string? observed)
    {
        if (string.Equals(existing, observed, StringComparison.Ordinal))
            return true;
        if (existing is null || observed is null)
            return false;
        if (
            !existing.TryCreatePhotoRootIdentity(out var existingIdentity, out _, out _)
            || !observed.TryCreatePhotoRootIdentity(out var observedIdentity, out _, out _)
        )
            return false;
        return string.Equals(existingIdentity, observedIdentity, StringComparison.Ordinal);
    }

    private static List<PlexPhotoFolder> BuildPhotoFolders(
        IReadOnlyCollection<PlexPhotoFolder> roots,
        IReadOnlyCollection<PlexPhotoMediaData> assets,
        PlexLibrary library,
        ILogger log
    )
    {
        var allFolders = new List<PlexPhotoFolder>();
        foreach (var root in roots)
        {
            root.PlexLibraryId = library.Id;
            root.PlexServerId = library.PlexServerId;
        }

        var foldersByIdentity = roots.ToDictionary(x => (x.RootIdentityKey!, x.RelativePath), x => x);
        foreach (var asset in assets)
        {
            var sourcePath = asset.SourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                log.Here()
                    .Debug(
                        "Photo asset {PlexApiRatingKey} has no Plex source path; retaining it without a folder",
                        asset.PlexApiRatingKey
                    );
                continue;
            }

            if (!sourcePath.TryResolvePhotoSourcePath(roots, out var match))
            {
                log.Here()
                    .Warning(
                        "Unable to map photo asset {PlexApiRatingKey} source path {SourcePath}; retaining it without a folder",
                        asset.PlexApiRatingKey,
                        sourcePath
                    );
                continue;
            }

            var relativeDirectorySeparator = match.RelativePath.LastIndexOf('/');
            var canonicalDirectorySeparator = match.CanonicalRelativePath.LastIndexOf('/');
            var relativeDirectory =
                relativeDirectorySeparator < 0 ? string.Empty : match.RelativePath[..relativeDirectorySeparator];
            var canonicalRelativeDirectory =
                canonicalDirectorySeparator < 0
                    ? string.Empty
                    : match.CanonicalRelativePath[..canonicalDirectorySeparator];
            var canonicalSegments = string.IsNullOrEmpty(canonicalRelativeDirectory)
                ? []
                : canonicalRelativeDirectory.Split('/');
            var displaySegments = string.IsNullOrEmpty(relativeDirectory) ? [] : relativeDirectory.Split('/');
            var current = match.Root;
            for (var index = 0; index < canonicalSegments.Length; index++)
            {
                var relativePath = string.IsNullOrEmpty(current.RelativePath)
                    ? canonicalSegments[index]
                    : $"{current.RelativePath}/{canonicalSegments[index]}";
                var key = (match.Root.RootIdentityKey!, relativePath);
                if (!foldersByIdentity.TryGetValue(key, out var folder))
                {
                    folder = new PlexPhotoFolder
                    {
                        Id = 0,
                        PlexLibraryId = library.Id,
                        PlexServerId = library.PlexServerId,
                        Parent = current,
                        RelativePath = relativePath,
                        Title = displaySegments[index],
                    };
                    current.Children.Add(folder);
                    foldersByIdentity.Add(key, folder);
                    allFolders.Add(folder);
                }

                current = folder;
            }

            asset.PlexPhotoFolder = current;
            current.Files.Add(asset);
        }

        return allFolders;
    }

    private static Result<long> ParsePhotoSectionId(string key) =>
        long.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? Result.Ok(value)
            : Result.Fail<long>($"Photo library key '{key}' is not numeric").LogError();

    private static int ParsePhotoRatingKey(Metadata metadata) =>
        int.TryParse(metadata.RatingKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException("Validated photo metadata has no positive rating key");

    private static bool IsPositivePhotoRatingKey(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0;

    private static bool IsPhotoChildrenKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Split('?', 2)[0].TrimEnd('/').EndsWith("/children", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetPhotoAlbumNavigation(string? key, out PhotoAlbumNavigation navigation)
    {
        navigation = null!;
        if (!TryGetReturnedPhotoRoute(key, out var path, out var query))
            return false;

        const string prefix = "/library/metadata/";
        const string suffix = "/children";
        if (
            !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        )
            return false;

        var idText = path[prefix.Length..^suffix.Length];
        if (
            idText.Contains("/", StringComparison.Ordinal)
            || !int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestId)
            || requestId <= 0
        )
            return false;

        navigation = new PhotoAlbumNavigation(requestId, query, BuildPhotoNavigationIdentity(path, query));
        return true;
    }

    private static bool TryGetPhotoFolderNavigation(string? key, long sectionId, out PhotoFolderNavigation navigation)
    {
        navigation = null!;
        if (!TryGetReturnedPhotoRoute(key, out var path, out var query))
            return false;

        var expectedPath = $"/library/sections/{sectionId}/folder";
        if (!string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase))
            return false;

        var parentArguments = query
            .Where(x => string.Equals(x.Key, "parent", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (parentArguments.Count != 1 || string.IsNullOrWhiteSpace(parentArguments[0].Value))
            return false;

        var additionalQuery = query
            .Where(x => !string.Equals(x.Key, "parent", StringComparison.OrdinalIgnoreCase))
            .ToList();
        navigation = new PhotoFolderNavigation(
            parentArguments[0].Value,
            additionalQuery,
            BuildPhotoNavigationIdentity(path, query)
        );
        return true;
    }

    private static bool TryGetReturnedPhotoRoute(
        string? key,
        out string path,
        out List<KeyValuePair<string, string>> query
    )
    {
        path = string.Empty;
        query = [];
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("/", StringComparison.Ordinal))
            return false;

        var queryIndex = key.IndexOf('?', StringComparison.Ordinal);
        path = (queryIndex < 0 ? key : key[..queryIndex]).TrimEnd('/');
        if (path.Length == 0)
            return false;
        if (queryIndex < 0)
            return true;

        var rawQuery = key[(queryIndex + 1)..];
        if (rawQuery.Length == 0)
            return false;
        foreach (var parameter in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = parameter.Split('=', 2);
            if (parts.Length != 2)
                return false;

            string name;
            string value;
            try
            {
                name = Uri.UnescapeDataString(parts[0]);
                value = Uri.UnescapeDataString(parts[1]);
            }
            catch (UriFormatException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(name) || IsPhotoCredentialArgument(name))
                return false;
            if (IsPhotoPagingArgument(name))
                continue;
            query.Add(new KeyValuePair<string, string>(name, value));
        }

        return true;
    }

    private static string BuildPhotoNavigationIdentity(
        string path,
        IReadOnlyCollection<KeyValuePair<string, string>> query
    ) =>
        query.Count == 0
            ? path
            : $"{path}?{string.Join(
                "&",
                query
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Key, StringComparer.Ordinal)
                    .ThenBy(x => x.Value, StringComparer.Ordinal)
                    .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
            )}";

    private static bool IsPhotoPagingArgument(string name) =>
        name.Equals("X-Plex-Container-Start", StringComparison.OrdinalIgnoreCase)
        || name.Equals("X-Plex-Container-Size", StringComparison.OrdinalIgnoreCase);

    private static bool IsPhotoCredentialArgument(string name) =>
        name.Equals("X-Plex-Token", StringComparison.OrdinalIgnoreCase)
        || name.Equals("token", StringComparison.OrdinalIgnoreCase)
        || name.Equals("authToken", StringComparison.OrdinalIgnoreCase);

    private const string PhotoRootParentToken = "<root>";

    private sealed record PhotoFolderNavigation(
        string? Parent,
        IReadOnlyList<KeyValuePair<string, string>> AdditionalQuery,
        string Identity
    );

    private sealed record PhotoAlbumNavigation(
        int RequestId,
        IReadOnlyList<KeyValuePair<string, string>> AdditionalQuery,
        string Identity
    );

    private sealed class PhotoCatalogState(long sectionId)
    {
        public long SectionId { get; } = sectionId;
        public Dictionary<int, PlexPhotoAlbum> Albums { get; } = [];
        public Dictionary<int, PhotoAssetState> Assets { get; } = [];
        public Dictionary<string, PhotoFolderNavigation> PendingFolderParents { get; } =
            new() { [PhotoRootParentToken] = new(null, [], PhotoRootParentToken) };
        public HashSet<string> AlbumNavigationKeys { get; } = [];
        public HashSet<string> ActiveFolderParents { get; } = [];
        public HashSet<string> CompletedFolderParents { get; } = [];
    }

    private sealed class PhotoAssetState(PlexPhotoMediaData entity)
    {
        public PlexPhotoMediaData Entity { get; } = entity;
        public HashSet<int> AlbumKeys { get; } = [];
    }
}
