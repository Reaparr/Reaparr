using System.Globalization;
using System.Net;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using Reaparr.Application.Contracts;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public sealed class GetLibraryMediaPhotoGraphUnitTests : BaseUnitTest<GetLibraryMediaFromPlexApiCommandHandler>
{
    [Test]
    [Arguments(PhotoResponseMode.Normal)]
    [Arguments(PhotoResponseMode.NoTotalChildren)]
    [Arguments(PhotoResponseMode.NoTotalPages)]
    [Arguments(PhotoResponseMode.NoPagingMetadata)]
    [Arguments(PhotoResponseMode.AlbumChildrenTokenDiffers)]
    [Arguments(PhotoResponseMode.ReturnedNavigationQuery)]
    [Arguments(PhotoResponseMode.ReturnedNavigationQueryPaged)]
    [Arguments(PhotoResponseMode.EquivalentWindowsPaths)]
    [Arguments(PhotoResponseMode.EquivalentUncPaths)]
    [Arguments(PhotoResponseMode.UnmappedAsset)]
    [Arguments(PhotoResponseMode.MissingSource)]
    public async Task ShouldBuildCompleteTypedPhotoGraph_WhenAlbumsOverlapAndSourcesVary(PhotoResponseMode mode)
    {
        // Arrange
        await SetupDatabase(9150, x => x.PlexPhotoLibraryCount = 1);
        var original = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var section = CreateLibrary(original);
        var requests = new List<(string Path, int Offset, string? Type)>();
        var preservedQueries = new List<string>();
        var transport = new Mock<IPlexApiClient>(MockBehavior.Strict);
        var sdk = new PlexAPI(client: transport.Object, serverUrl: "http://plex.test", token: "test-token");
        var expectedRequestCount = mode switch
        {
            PhotoResponseMode.NoTotalChildren => 11,
            PhotoResponseMode.NoTotalPages or PhotoResponseMode.ReturnedNavigationQueryPaged => 18,
            _ => 7,
        };

        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == original.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { section }))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<GetLibraryMediaTotalsCommand>(x => x.PlexLibrary.Id == original.Id))
            .ReturnsAsync(Result.Ok<IReadOnlyDictionary<PlexMediaType, int>>(new Dictionary<PlexMediaType, int>()))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(original.Id, original.Type, It.IsAny<IReadOnlyDictionary<PlexMediaType, int>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        transport
            .Setup(x => x.CloneAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync((HttpRequestMessage request) => request);
        transport
            .Setup(x =>
                x.SendAsync(
                    It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.Host == "plex.test")
                )
            )
            .ReturnsAsync(
                (HttpRequestMessage request) => CreateResponse(request, original.Key, mode, requests, preservedQueries)
            )
            .Verifiable(Times.Exactly(expectedRequestCount));
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClientAsync(original.PlexServerId, 30, 3, null))
            .ReturnsAsync(Result.Ok<IPlexAPI>(sdk))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(original), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var library = result.Value.Library;
        library.PhotoAlbums.Select(x => x.PlexApiRatingKey).ShouldBe([501, 502]);
        library
            .PhotoAlbums.SelectMany(album =>
                album.Memberships.Select(m => (album.PlexApiRatingKey, m.PlexPhotoMediaData!.PlexApiRatingKey))
            )
            .OrderBy(x => x.Item1)
            .ThenBy(x => x.Item2)
            .ShouldBe([(501, 601), (501, 602), (502, 601), (502, 602)]);
        var photoPath = mode switch
        {
            PhotoResponseMode.UnmappedAsset => "/elsewhere/photo.jpg",
            PhotoResponseMode.MissingSource => null,
            PhotoResponseMode.EquivalentWindowsPaths => @"C:\Photos\Family\photo.jpg",
            PhotoResponseMode.EquivalentUncPaths => @"\\SERVER\SHARE\Photos\photo.jpg",
            _ => "/photos/Family/photo.jpg",
        };
        var photoFolder = mode
            is PhotoResponseMode.UnmappedAsset
                or PhotoResponseMode.MissingSource
                or PhotoResponseMode.EquivalentWindowsPaths
                or PhotoResponseMode.EquivalentUncPaths
            ? null
            : "Family";
        library
            .PhotoMediaData.OrderBy(x => x.PlexApiRatingKey)
            .Select(x =>
                (
                    x.PlexApiRatingKey,
                    x.AssetKind,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.SourcePath,
                    x.OriginalFilename,
                    x.Size,
                    x.PlexPhotoFolder?.RelativePath
                )
            )
            .ShouldBe([
                (
                    601,
                    PhotoAssetKind.Image,
                    700,
                    701,
                    photoPath,
                    mode == PhotoResponseMode.MissingSource ? "" : "photo.jpg",
                    12L,
                    photoFolder
                ),
                (602, PhotoAssetKind.VideoClip, 800, 801, "/photos/clip.mp4", "clip.mp4", 24L, ""),
            ]);
        library
            .PhotoMediaData.Select(x => (x.PlexLibraryId, x.PlexServerId))
            .ShouldBe([(original.Id, original.PlexServerId), (original.Id, original.PlexServerId)]);
        var photo = library.PhotoMediaData.Single(x => x.PlexApiRatingKey == 601);
        (photo.Key, photo.AddedAt, photo.OriginallyAvailableAt).ShouldBe(
            ("/library/parts/701/file.jpg", DateTime.UnixEpoch.AddSeconds(1), new DateTime(2026, 4, 18, 12, 34, 56))
        );
        var clip = library.PhotoMediaData.Single(x => x.PlexApiRatingKey == 602);
        (clip.Key, clip.Width, clip.Height, clip.Duration, clip.VideoCodec, clip.AudioCodec).ShouldBe(
            ("/library/parts/801/file.mp4", 1920, 1080, 2500, "h264", "aac")
        );
        result.Value.PhotoClipCount.ShouldBe(1);
        var folders = library
            .PhotoFolders.OrderBy(x => x.RelativePath)
            .Select(x => (x.RelativePath, x.Parent?.RelativePath, x.RootIdentityKey))
            .ToList();
        if (photoFolder is null)
            folders.ShouldBe([("", null, "path:posix:/photos")]);
        else
            folders.ShouldBe([("", null, "path:posix:/photos"), ("Family", "", null)]);
        var children = requests.Where(x => x.Path.EndsWith("/children", StringComparison.Ordinal)).ToList();
        var firstAlbumPath =
            mode == PhotoResponseMode.AlbumChildrenTokenDiffers
                ? "/library/metadata/9501/children"
                : "/library/metadata/501/children";
        var secondAlbumPath =
            mode == PhotoResponseMode.AlbumChildrenTokenDiffers
                ? "/library/metadata/9502/children"
                : "/library/metadata/502/children";
        if (
            mode
            is PhotoResponseMode.NoTotalChildren
                or PhotoResponseMode.NoTotalPages
                or PhotoResponseMode.ReturnedNavigationQueryPaged
        )
            children.ShouldBe([
                (firstAlbumPath, 0, null),
                (firstAlbumPath, 1, null),
                (firstAlbumPath, 2, null),
                (secondAlbumPath, 0, null),
                (secondAlbumPath, 1, null),
                (secondAlbumPath, 2, null),
            ]);
        else
            children.ShouldBe([(firstAlbumPath, 0, null), (secondAlbumPath, 0, null)]);
        if (mode is PhotoResponseMode.NoTotalPages or PhotoResponseMode.ReturnedNavigationQueryPaged)
        {
            var allPath = $"/library/sections/{original.Key}/all";
            requests
                .Where(x => x.Path == allPath && x.Type is "13" or "photo")
                .Select(x => x.Offset)
                .ShouldBe([0, 1, 2]);
            requests
                .Where(x => x.Path == allPath && x.Type is "14" or "photoalbum")
                .Select(x => x.Offset)
                .ShouldBe([0, 1, 2]);
            requests.Where(x => x.Path == allPath && x.Type is "12" or "clip").Select(x => x.Offset).ShouldBe([0, 1]);
            requests
                .Where(x => x.Path.EndsWith("/folder", StringComparison.Ordinal))
                .Select(x => x.Offset)
                .ShouldBe([0, 1, 0, 1]);
        }
        if (mode is PhotoResponseMode.ReturnedNavigationQuery or PhotoResponseMode.ReturnedNavigationQueryPaged)
        {
            var albumPageCount = mode == PhotoResponseMode.ReturnedNavigationQuery ? 2 : 6;
            var folderPageCount = mode == PhotoResponseMode.ReturnedNavigationQuery ? 1 : 2;
            var forwardedNames = new HashSet<string>(
                ["view", "album", "section", "includeMeta"],
                StringComparer.OrdinalIgnoreCase
            );
            preservedQueries
                .Select(query =>
                    string.Join(
                        "&",
                        query
                            .TrimStart('?')
                            .Split('&')
                            .Where(pair => forwardedNames.Contains(pair.Split('=', 2)[0]))
                            .OrderBy(pair => pair, StringComparer.Ordinal)
                    )
                )
                .ShouldBe(
                    Enumerable
                        .Repeat("album=curated&includeMeta=0&view=full&view=thumb", albumPageCount)
                        .Concat(Enumerable.Repeat("includeMeta=0&section=photos&view=full&view=thumb", folderPageCount))
                );
            foreach (var query in preservedQueries)
            {
                query
                    .TrimStart('?')
                    .Split('&')
                    .Where(pair => pair.StartsWith("X-Plex-Container-Size=", StringComparison.OrdinalIgnoreCase))
                    .ShouldBe(["X-Plex-Container-Size=100"]);
            }
        }
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
        transport.Verify();
    }

    [Test]
    [Arguments(PhotoResponseMode.HistoricalSection10, 129, 2, 8)]
    [Arguments(PhotoResponseMode.RootOnlyOne, 1, 0, 4)]
    [Arguments(PhotoResponseMode.RootOnlySeventeen, 17, 0, 4)]
    public async Task ShouldPreserveHistoricalPhotoShapesAndRootOnlyLibraries(
        PhotoResponseMode mode,
        int expectedAssetCount,
        int expectedAlbumCount,
        int expectedRequestCount
    )
    {
        // Arrange
        await SetupDatabase(9152, x => x.PlexPhotoLibraryCount = 1);
        var original = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var section = CreateLibrary(original);
        var requests = new List<(string Path, int Offset, string? Type)>();
        var transport = new Mock<IPlexApiClient>(MockBehavior.Strict);
        var sdk = new PlexAPI(client: transport.Object, serverUrl: "http://plex.test", token: "test-token");

        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == original.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { section }))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<GetLibraryMediaTotalsCommand>(x => x.PlexLibrary.Id == original.Id))
            .ReturnsAsync(Result.Ok<IReadOnlyDictionary<PlexMediaType, int>>(new Dictionary<PlexMediaType, int>()))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(original.Id, original.Type, It.IsAny<IReadOnlyDictionary<PlexMediaType, int>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        transport
            .Setup(x => x.CloneAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync((HttpRequestMessage request) => request);
        transport
            .Setup(x =>
                x.SendAsync(
                    It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.Host == "plex.test")
                )
            )
            .ReturnsAsync(
                (HttpRequestMessage request) => CreateHistoricalResponse(request, original.Key, mode, requests)
            )
            .Verifiable(Times.Exactly(expectedRequestCount));
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClientAsync(original.PlexServerId, 30, 3, null))
            .ReturnsAsync(Result.Ok<IPlexAPI>(sdk))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(original), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var expectedRatingKeys =
            mode == PhotoResponseMode.HistoricalSection10
                ? Enumerable.Range(10000, 111).Concat(Enumerable.Range(20000, 17)).Concat([30000])
                : Enumerable.Range(10000, expectedAssetCount);
        result
            .Value.Library.PhotoMediaData.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => x.PlexApiRatingKey)
            .ShouldBe(expectedRatingKeys);
        result.Value.Library.PhotoAlbums.Count.ShouldBe(expectedAlbumCount);
        if (mode == PhotoResponseMode.HistoricalSection10)
            result
                .Value.Library.PhotoAlbums.SelectMany(album =>
                    album.Memberships.Select(edge =>
                        (album.PlexApiRatingKey, edge.PlexPhotoMediaData!.PlexApiRatingKey)
                    )
                )
                .OrderBy(edge => edge.Item1)
                .ThenBy(edge => edge.Item2)
                .ShouldBe(Enumerable.Range(20000, 17).Select(key => (99133, key)).Concat([(99151, 30000)]));
        else
            result.Value.Library.PhotoAlbums.ShouldBeEmpty();
        requests.Count.ShouldBe(expectedRequestCount);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
        transport.Verify();
    }

    [Test]
    [Arguments(PhotoResponseMode.BadWindowChildren, 3, "total")]
    [Arguments(PhotoResponseMode.IncompleteChildren, 3, "incomplete")]
    [Arguments(PhotoResponseMode.RepeatedWindowChildren, 4, "repeated nonempty")]
    [Arguments(PhotoResponseMode.UnpagedAfterPaged, 4, "unpaged")]
    [Arguments(PhotoResponseMode.PosixDistinctPaths, 4, "conflicting single-file")]
    [Arguments(PhotoResponseMode.MultipleParts, 1, "exactly one")]
    [Arguments(PhotoResponseMode.MultipleMedia, 1, "exactly one")]
    [Arguments(PhotoResponseMode.WrongLibrary, 1, "another library")]
    [Arguments(PhotoResponseMode.CyclicFolder, 7, "cyclic")]
    [Arguments(PhotoResponseMode.BadOffsetMedia, 1, "offset")]
    [Arguments(PhotoResponseMode.IncompleteFolder, 6, "incomplete")]
    public async Task ShouldRejectIncompleteOrUnsupportedPhotoGraph_BeforePublishingIt(
        PhotoResponseMode mode,
        int expectedRequestCount,
        string expectedError
    )
    {
        // Arrange
        await SetupDatabase(9151, x => x.PlexPhotoLibraryCount = 1);
        var original = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var section = CreateLibrary(original);
        original.PhotoMediaData.Count.ShouldBe(0);
        original.PhotoAlbums.Count.ShouldBe(0);
        var requests = new List<(string Path, int Offset, string? Type)>();
        var transport = new Mock<IPlexApiClient>(MockBehavior.Strict);
        var sdk = new PlexAPI(client: transport.Object, serverUrl: "http://plex.test", token: "test-token");

        Mock.SetupCommand(() => It.Is<GetLibrarySectionsCommand>(x => x.PlexServerId == original.PlexServerId))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary> { section }))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<GetLibraryMediaTotalsCommand>(x => x.PlexLibrary.Id == original.Id))
            .ReturnsAsync(Result.Ok<IReadOnlyDictionary<PlexMediaType, int>>(new Dictionary<PlexMediaType, int>()))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.StartAsync(original.Id, original.Type, It.IsAny<IReadOnlyDictionary<PlexMediaType, int>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateErrorAsync(
                    original.Id,
                    It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1),
                    It.Is<CancellationToken>(ct => ct == CancellationToken)
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        transport
            .Setup(x => x.CloneAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync((HttpRequestMessage request) => request);
        transport
            .Setup(x =>
                x.SendAsync(
                    It.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.Host == "plex.test")
                )
            )
            .ReturnsAsync((HttpRequestMessage request) => CreateResponse(request, original.Key, mode, requests))
            .Verifiable(Times.Exactly(expectedRequestCount));
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClientAsync(original.PlexServerId, 30, 3, null))
            .ReturnsAsync(Result.Ok<IPlexAPI>(sdk))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaFromPlexApiCommand(original), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain(expectedError);
        original.PhotoMediaData.Count.ShouldBe(0);
        original.PhotoAlbums.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
        transport.Verify();
    }

    private static PlexLibrary CreateLibrary(PlexLibrary source)
    {
        var library = new PlexLibrary
        {
            Id = 0,
            PlexServerId = source.PlexServerId,
            Key = source.Key,
            Type = PlexMediaType.PhotoAlbum,
            Title = source.Title,
            Uuid = source.Uuid,
            Language = source.Language,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            ScannedAt = source.ScannedAt,
            ContentChangedAt = source.ContentChangedAt,
        };
        library.PhotoFolders.Add(
            new PlexPhotoFolder
            {
                PlexServerId = source.PlexServerId,
                PlexLibraryId = 0,
                RelativePath = string.Empty,
                Title = "photos",
                RootIdentityKey = "path:posix:/photos",
                SourceRootPath = "/photos",
                IsActive = true,
            }
        );
        return library;
    }

    private static HttpResponseMessage CreateResponse(
        HttpRequestMessage request,
        string sectionKey,
        PhotoResponseMode mode,
        List<(string Path, int Offset, string? Type)> requests,
        List<string>? preservedQueries = null
    )
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = request.ParseQueryToDictionary();
        if (
            mode is PhotoResponseMode.ReturnedNavigationQuery or PhotoResponseMode.ReturnedNavigationQueryPaged
            && query.ContainsKey("view")
            && (
                path.EndsWith("/folder", StringComparison.Ordinal)
                || path.EndsWith("/children", StringComparison.Ordinal)
            )
        )
            preservedQueries?.Add(request.RequestUri.Query);
        var offset = query.TryGetValue("X-Plex-Container-Start", out var start)
            ? int.Parse(start, CultureInfo.InvariantCulture)
            : 0;
        query.TryGetValue("type", out var type);
        requests.Add((path, offset, type));
        var sectionId = long.Parse(sectionKey, CultureInfo.InvariantCulture);
        var photoSourceOverride = mode switch
        {
            PhotoResponseMode.EquivalentWindowsPaths
                when path.Contains("/metadata/502/children", StringComparison.Ordinal) => @"c:/photos/family/PHOTO.JPG",
            PhotoResponseMode.EquivalentWindowsPaths => @"C:\Photos\Family\photo.jpg",
            PhotoResponseMode.EquivalentUncPaths
                when path.Contains("/metadata/502/children", StringComparison.Ordinal) =>
                "//server/share/photos/PHOTO.JPG",
            PhotoResponseMode.EquivalentUncPaths => @"\\SERVER\SHARE\Photos\photo.jpg",
            PhotoResponseMode.PosixDistinctPaths
                when path.Contains("/metadata/502/children", StringComparison.Ordinal) => "/photos/family/photo.jpg",
            _ => null,
        };
        var photo = CreateAsset(601, 700, 701, sectionId, mode, photoSourceOverride);
        var clip = CreateAsset(602, 800, 801, sectionId, mode);
        var container = new MediaContainerWithMetadataMediaContainer
        {
            Offset = offset,
            TotalSize = 0,
            Size = 0,
        };
        if (path.EndsWith("/children", StringComparison.Ordinal))
        {
            container.TotalSize = mode switch
            {
                PhotoResponseMode.NoTotalChildren or PhotoResponseMode.UnpagedAfterPaged => null,
                PhotoResponseMode.RepeatedWindowChildren => 3,
                _ => 2,
            };
            if (mode == PhotoResponseMode.BadWindowChildren)
                container.TotalSize = 0;
            if (mode != PhotoResponseMode.IncompleteChildren)
            {
                if (mode is PhotoResponseMode.NoTotalChildren or PhotoResponseMode.UnpagedAfterPaged)
                {
                    if (offset == 0)
                        container.Photo = [photo];
                    else if (offset == 1)
                        container.Video = [clip];
                }
                else if (mode == PhotoResponseMode.RepeatedWindowChildren)
                    container.Photo = [photo];
                else
                {
                    container.Photo = [photo];
                    container.Video = [clip];
                }
            }
        }
        else if (path.EndsWith("/folder", StringComparison.Ordinal))
        {
            if (!query.ContainsKey("parent") || mode == PhotoResponseMode.CyclicFolder)
                container.Directory = [CreateFolder(sectionKey, sectionId, mode)];
            else
            {
                photo.Type = "photo";
                container.Metadata = [photo];
            }
        }
        else if (path == $"/library/sections/{sectionKey}/all")
        {
            if (type is "13" or "photo")
            {
                container.Directory = [CreateFolder(sectionKey, sectionId, mode)];
                container.Photo = [photo];
            }
            else if (type is "14" or "photoalbum")
                container.Metadata = [CreateAlbum(501, sectionId, mode), CreateAlbum(502, sectionId, mode)];
            else if (type is "12" or "clip")
                container.Video = [clip];
            else
                throw new InvalidOperationException($"Unexpected photo selector {type}");
        }
        else
            throw new InvalidOperationException($"Unexpected SDK route {path}");
        if (mode is PhotoResponseMode.NoTotalPages or PhotoResponseMode.ReturnedNavigationQueryPaged)
        {
            var remaining = offset;
            container.Directory = TakePage(container.Directory, ref remaining);
            container.Metadata = TakePage(container.Metadata, ref remaining);
            container.Photo = TakePage(container.Photo, ref remaining);
            container.Video = TakePage(container.Video, ref remaining);
        }
        container.Size =
            (container.Metadata?.Count ?? 0)
            + (container.Directory?.Count ?? 0)
            + (container.Photo?.Count ?? 0)
            + (container.Video?.Count ?? 0);
        if (!path.EndsWith("/children", StringComparison.Ordinal))
            container.TotalSize = container.Size;
        if (
            mode
            is PhotoResponseMode.NoTotalPages
                or PhotoResponseMode.NoPagingMetadata
                or PhotoResponseMode.ReturnedNavigationQueryPaged
        )
            container.TotalSize = null;
        if (mode == PhotoResponseMode.NoPagingMetadata)
            container.Offset = null;
        if (mode == PhotoResponseMode.BadOffsetMedia && path.EndsWith("/all", StringComparison.Ordinal))
            container.Offset = offset + 1;
        if (
            mode == PhotoResponseMode.UnpagedAfterPaged
            && path.EndsWith("/children", StringComparison.Ordinal)
            && offset > 0
        )
            container.Offset = null;
        if (mode == PhotoResponseMode.IncompleteFolder && path.EndsWith("/folder", StringComparison.Ordinal))
        {
            container.Directory = null;
            container.Metadata = null;
            container.Size = 0;
            container.TotalSize = 1;
        }
        return new MediaContainerWithMetadata { MediaContainer = container }.ToJsonHttpResponse(
            request,
            HttpStatusCode.OK
        );
    }

    private static HttpResponseMessage CreateHistoricalResponse(
        HttpRequestMessage request,
        string sectionKey,
        PhotoResponseMode mode,
        List<(string Path, int Offset, string? Type)> requests
    )
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = request.ParseQueryToDictionary();
        var offset = query.TryGetValue("X-Plex-Container-Start", out var start)
            ? int.Parse(start, CultureInfo.InvariantCulture)
            : 0;
        query.TryGetValue("type", out var type);
        requests.Add((path, offset, type));

        var sectionId = long.Parse(sectionKey, CultureInfo.InvariantCulture);
        var container = new MediaContainerWithMetadataMediaContainer
        {
            Offset = offset,
            TotalSize = 0,
            Size = 0,
        };
        if (path.EndsWith("/children", StringComparison.Ordinal))
        {
            var children = path.Contains("/metadata/99133/children", StringComparison.Ordinal)
                ? CreateHistoricalAssets(20000, 17, sectionId, "/photos/Folder A")
                : CreateHistoricalAssets(30000, 1, sectionId, "/photos/Album B");
            SetHistoricalPhotoPage(container, children, offset);
        }
        else if (path.EndsWith("/folder", StringComparison.Ordinal))
        {
            if (mode == PhotoResponseMode.HistoricalSection10 && query.ContainsKey("parent"))
                SetHistoricalPhotoPage(
                    container,
                    CreateHistoricalAssets(20000, 17, sectionId, "/photos/Folder A"),
                    offset
                );
            else if (mode == PhotoResponseMode.HistoricalSection10)
            {
                container.Directory = [CreateHistoricalFolder(sectionKey, sectionId)];
                container.Size = 1;
                container.TotalSize = 1;
            }
        }
        else if (path == $"/library/sections/{sectionKey}/all")
        {
            if (type is "13" or "photo")
            {
                if (mode == PhotoResponseMode.HistoricalSection10)
                {
                    var all = new List<Metadata> { CreateHistoricalFolder(sectionKey, sectionId) };
                    all.AddRange(CreateHistoricalAssets(10000, 111, sectionId, "/photos"));
                    SetHistoricalMixedPage(container, all, offset);
                }
                else
                {
                    var rootCount = mode == PhotoResponseMode.RootOnlyOne ? 1 : 17;
                    SetHistoricalPhotoPage(
                        container,
                        CreateHistoricalAssets(10000, rootCount, sectionId, "/photos"),
                        offset
                    );
                }
            }
            else if (type is "14" or "photoalbum")
            {
                container.Metadata =
                    mode == PhotoResponseMode.HistoricalSection10
                        ? [CreateAlbum(99133, sectionId, mode), CreateAlbum(99151, sectionId, mode)]
                        : [];
                container.Size = container.Metadata.Count;
                container.TotalSize = container.Metadata.Count;
            }
        }

        return new MediaContainerWithMetadata { MediaContainer = container }.ToJsonHttpResponse(
            request,
            HttpStatusCode.OK
        );
    }

    private static void SetHistoricalPhotoPage(
        MediaContainerWithMetadataMediaContainer container,
        IReadOnlyList<Metadata> all,
        int offset
    )
    {
        var page = all.Skip(offset).Take(100).ToList();
        container.Photo = page;
        container.Size = page.Count;
        container.TotalSize = all.Count;
    }

    private static void SetHistoricalMixedPage(
        MediaContainerWithMetadataMediaContainer container,
        IReadOnlyList<Metadata> all,
        int offset
    )
    {
        var page = all.Skip(offset).Take(100).ToList();
        container.Directory = page.Where(x => string.IsNullOrWhiteSpace(x.RatingKey)).ToList();
        container.Photo = page.Where(x => !string.IsNullOrWhiteSpace(x.RatingKey)).ToList();
        container.Size = page.Count;
        container.TotalSize = all.Count;
    }

    private static List<Metadata> CreateHistoricalAssets(
        int firstRatingKey,
        int count,
        long sectionId,
        string sourceDirectory
    ) =>
        Enumerable
            .Range(firstRatingKey, count)
            .Select(ratingKey => new Metadata
            {
                RatingKey = ratingKey.ToString(CultureInfo.InvariantCulture),
                Key = $"/library/metadata/{ratingKey}",
                Type = "photo",
                Title = $"Photo {ratingKey}",
                LibrarySectionID = sectionId,
                AddedAt = 1,
                SkipChildren = SkipChildren.CreateBoolean(false),
                SkipParent = SkipParent.CreateBoolean(false),
                Media =
                [
                    new Media
                    {
                        Id = ratingKey + 100000,
                        Width = 800,
                        Height = 600,
                        Part =
                        [
                            new Part
                            {
                                Id = ratingKey + 200000,
                                Key = $"/library/parts/{ratingKey + 200000}/file.jpg",
                                File = $"{sourceDirectory}/photo-{ratingKey}.jpg",
                                Size = 12,
                                Container = "jpg",
                            },
                        ],
                    },
                ],
            })
            .ToList();

    private static Metadata CreateHistoricalFolder(string sectionKey, long sectionId) =>
        new()
        {
            Key = $"/library/sections/{sectionKey}/folder?parent=3706",
            Type = "directory",
            Title = "Folder A",
            LibrarySectionID = sectionId,
            SkipChildren = SkipChildren.CreateBoolean(false),
            SkipParent = SkipParent.CreateBoolean(false),
        };

    private static List<Metadata>? TakePage(List<Metadata>? items, ref int remaining)
    {
        if (items is null || remaining < 0)
            return null;
        if (remaining >= items.Count)
        {
            remaining -= items.Count;
            return null;
        }
        var item = items[remaining];
        remaining = -1;
        return [item];
    }

    private static Metadata CreateFolder(string sectionKey, long sectionId, PhotoResponseMode mode) =>
        new()
        {
            Key =
                $"/library/sections/{sectionKey}/folder?parent=3706"
                + (
                    mode is PhotoResponseMode.ReturnedNavigationQuery or PhotoResponseMode.ReturnedNavigationQueryPaged
                        ? "&view=full&view=thumb&section=photos&includeMeta=0"
                        : string.Empty
                ),
            LibrarySectionID = sectionId,
            SkipChildren = SkipChildren.CreateBoolean(false),
            SkipParent = SkipParent.CreateBoolean(false),
        };

    private static Metadata CreateAlbum(int id, long sectionId, PhotoResponseMode mode) =>
        new()
        {
            RatingKey = id.ToString(CultureInfo.InvariantCulture),
            Key =
                $"/library/metadata/{(mode == PhotoResponseMode.AlbumChildrenTokenDiffers ? id + 9000 : id)}/children"
                + (
                    mode is PhotoResponseMode.ReturnedNavigationQuery or PhotoResponseMode.ReturnedNavigationQueryPaged
                        ? "?view=full&view=thumb&album=curated&includeMeta=0"
                        : string.Empty
                ),
            Type = "photoalbum",
            Title = $"Album {id}",
            LibrarySectionID = sectionId,
            SkipChildren = SkipChildren.CreateBoolean(false),
            SkipParent = SkipParent.CreateBoolean(false),
        };

    private static Metadata CreateAsset(
        int ratingKey,
        int mediaId,
        int partId,
        long sectionId,
        PhotoResponseMode mode,
        string? sourcePathOverride = null
    )
    {
        var image = ratingKey == 601;
        var sourcePath = sourcePathOverride ?? (image ? "/photos/Family/photo.jpg" : "/photos/clip.mp4");
        if (image && mode == PhotoResponseMode.UnmappedAsset)
            sourcePath = "/elsewhere/photo.jpg";
        if (image && mode == PhotoResponseMode.MissingSource)
            sourcePath = null;
        var part = new Part
        {
            Id = partId,
            Key = $"/library/parts/{partId}/file.{(image ? "jpg" : "mp4")}",
            File = sourcePath,
            Size = image ? 12 : 24,
            Container = image ? "jpg" : "mp4",
            Duration = image ? null : 2500,
        };
        var media = new Media
        {
            Id = mediaId,
            Part = image && mode == PhotoResponseMode.MultipleParts ? [part, part] : [part],
            Width = image ? 800 : 1920,
            Height = image ? 600 : 1080,
            VideoCodec = image ? null : "h264",
            AudioCodec = image ? null : "aac",
            HasVoiceActivity = HasVoiceActivity.CreateBoolean(false),
        };
        return new Metadata
        {
            RatingKey = ratingKey.ToString(CultureInfo.InvariantCulture),
            Key = $"/library/metadata/{ratingKey}",
            Title = image ? "Photo" : "Clip",
            LibrarySectionID = image && mode == PhotoResponseMode.WrongLibrary ? sectionId + 1 : sectionId,
            AddedAt = 1,
            OriginallyAvailableAt = "2026-04-18 12:34:56",
            SkipChildren = SkipChildren.CreateBoolean(false),
            SkipParent = SkipParent.CreateBoolean(false),
            Media = image && mode == PhotoResponseMode.MultipleMedia ? [media, media] : [media],
        };
    }

    public enum PhotoResponseMode
    {
        Normal,
        NoTotalChildren,
        NoTotalPages,
        NoPagingMetadata,
        RepeatedWindowChildren,
        UnpagedAfterPaged,
        AlbumChildrenTokenDiffers,
        ReturnedNavigationQuery,
        ReturnedNavigationQueryPaged,
        EquivalentWindowsPaths,
        EquivalentUncPaths,
        PosixDistinctPaths,
        HistoricalSection10,
        RootOnlyOne,
        RootOnlySeventeen,
        BadWindowChildren,
        IncompleteChildren,
        UnmappedAsset,
        MissingSource,
        MultipleParts,
        MultipleMedia,
        WrongLibrary,
        CyclicFolder,
        BadOffsetMedia,
        IncompleteFolder,
    }
}
