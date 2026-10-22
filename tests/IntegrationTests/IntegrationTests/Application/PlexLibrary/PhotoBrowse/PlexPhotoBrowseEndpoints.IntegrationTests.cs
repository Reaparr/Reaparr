using System.Text.Json;
using NaturalSort.Extension;

namespace Reaparr.IntegrationTests;

public class PlexPhotoBrowseEndpointsIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldExposeBoundedPhotoBrowseOperationsAndRejectDisabledLibrary()
    {
        // Arrange
        var seed = new Seed(29003);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (x, _) => x.SetupIdentityRequest(seed);
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoAlbumCount = 2;
                    x.PhotoCount = 3;
                    x.PhotoClipCount = 1;
                };
            }
        );
        var context = container.DbContext;
        var library = await context.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum, CancellationToken);
        var root = await context.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var albums = await context.PlexPhotoAlbums
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var assets = await context.PlexPhotoMediaData
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var album = albums[0];
        var asset = assets[0];
        var albumAssetIds = await context.PlexPhotoAlbumMemberships
            .Where(x => x.PlexPhotoAlbumId == album.Id)
            .OrderBy(x => x.PlexPhotoMediaDataId)
            .Select(x => x.PlexPhotoMediaDataId)
            .ToListAsync(CancellationToken);
        var searchTerm = "browseable";
        await context.PlexPhotoMediaData
            .Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, searchTerm), CancellationToken);

        using var client = container.GetApiClient();
        await client.SignIn();
        var fixture = new BrowseFixture(
            context,
            client,
            library,
            root,
            albums,
            assets,
            album,
            asset,
            albumAssetIds,
            searchTerm
        );

        // Act and Assert
        await AssertAlbumsAsync(fixture);
        await AssertAssetsAsync(fixture);
        await AssertRootsAsync(fixture);
        await AssertFolderAsync(fixture);
        await AssertMembersAsync(fixture);
        await AssertInspectionAsync(fixture);
        await AssertSearchAsync(fixture);
        await AssertInvalidSortAsync(fixture);
        await AssertDisabledLibraryAsync(fixture);
    }

    private static T AssertSuccess<T>(HttpStatusCode statusCode, ResultDTO<T> result)
        where T : class
    {
        statusCode.ShouldBe(HttpStatusCode.OK);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        return result.Value.ShouldNotBeNull();
    }

    private async Task AssertAlbumsAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            GetPhotoAlbumsEndpoint,
            GetPhotoAlbumsRequest,
            ResultDTO<PhotoAlbumPageDTO>
        >(
            new GetPhotoAlbumsRequest
            {
                PlexLibraryId = fixture.Library.Id,
                Page = 1,
                PageSize = 1,
                Sort = "title:asc",
            }
        );
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Items.Count.ShouldBe(1);
        value.TotalCount.ShouldBe(fixture.Albums.Count);
        value.Items[0].PlexLibraryId.ShouldBe(fixture.Library.Id);
        value.Items[0].AssetCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    private async Task AssertAssetsAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(
            new GetPhotoAssetsRequest
            {
                PlexLibraryId = fixture.Library.Id,
                Page = 1,
                PageSize = 2,
            }
        );
        var descendingResponse = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(
            new GetPhotoAssetsRequest
            {
                PlexLibraryId = fixture.Library.Id,
                Page = 1,
                PageSize = 2,
                Sort = "title:desc",
            }
        );
        var descendingValue = AssertSuccess(descendingResponse.Response.StatusCode, descendingResponse.Result);
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Items.Count.ShouldBe(Math.Min(2, fixture.Assets.Count));
        value.TotalCount.ShouldBe(fixture.Assets.Count);
        value.TotalBytes.ShouldBe(fixture.Assets.Sum(x => x.Size));
        value.Items.Select(x => x.Id).ShouldBe(
            fixture.Assets
                .OrderBy(x => x.Title, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
                .ThenBy(x => x.Id)
                .Take(2)
                .Select(x => x.Id)
        );
        value.Items.ShouldAllBe(x => x.SourceRootTitle == fixture.Root.Title);
        descendingValue.Items.Select(x => x.Id).ShouldBe(
            fixture.Assets
                .OrderByDescending(x => x.Title, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
                .ThenByDescending(x => x.Id)
                .Take(2)
                .Select(x => x.Id)
        );
    }

    private async Task AssertRootsAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            GetPhotoSourceRootsEndpoint,
            GetPhotoSourceRootsRequest,
            ResultDTO<PhotoFolderPageDTO>
        >(
            new GetPhotoSourceRootsRequest
            {
                PlexLibraryId = fixture.Library.Id,
                Page = 1,
                PageSize = 100,
            }
        );
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Items.Select(x => x.Id).ShouldContain(fixture.Root.Id);
    }

    private async Task AssertFolderAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            GetPhotoFolderChildrenEndpoint,
            GetPhotoFolderChildrenRequest,
            ResultDTO<PhotoFolderChildrenDTO>
        >(
            new GetPhotoFolderChildrenRequest
            {
                PlexLibraryId = fixture.Library.Id,
                FolderId = fixture.Root.Id,
            }
        );
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Folder.Id.ShouldBe(fixture.Root.Id);
        value.Breadcrumbs.Select(x => x.Id).ShouldBe([fixture.Root.Id]);
        value.Assets.TotalCount.ShouldBe(
            fixture.Assets.Count(x => x.PlexPhotoFolderId == fixture.Root.Id)
        );
    }

    private async Task AssertMembersAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            GetPhotoAlbumMembersEndpoint,
            GetPhotoAlbumMembersRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(
            new GetPhotoAlbumMembersRequest
            {
                PlexLibraryId = fixture.Library.Id,
                AlbumId = fixture.Album.Id,
            }
        );
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Items.Select(x => x.Id).ShouldBe(
            fixture.Assets
                .Where(x => fixture.AlbumAssetIds.Contains(x.Id))
                .OrderBy(x => x.Title, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
        );
    }

    private async Task AssertInspectionAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            GetPhotoAssetInspectionEndpoint,
            GetPhotoAssetInspectionRequest,
            ResultDTO<PhotoAssetInspectionDTO>
        >(
            new GetPhotoAssetInspectionRequest
            {
                PlexLibraryId = fixture.Library.Id,
                AssetId = fixture.Asset.Id,
            }
        );
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Id.ShouldBe(fixture.Asset.Id);
        value.PlexApiRatingKey.ShouldBe(fixture.Asset.PlexApiRatingKey);
        value.Albums.TotalCount.ShouldBe(
            fixture.AlbumAssetIds.Count(x => x == fixture.Asset.Id)
        );
        if (fixture.Asset.SourcePath is not null)
            value.RelativeFolderPath.ShouldNotBe(fixture.Asset.SourcePath);
    }

    private async Task AssertSearchAsync(BrowseFixture fixture)
    {
        var response = await fixture.Client.GETAsync<
            SearchPhotoAssetsEndpoint,
            SearchPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(
            new SearchPhotoAssetsRequest
            {
                Query = fixture.SearchTerm,
                Page = 1,
                PageSize = 100,
            }
        );
        var value = AssertSuccess(response.Response.StatusCode, response.Result);
        value.Items.Select(x => x.Id).ShouldContain(fixture.Asset.Id);
        value.Items.Select(x => x.Id).Distinct().Count().ShouldBe(
            value.Items.Count
        );
    }

    private async Task AssertInvalidSortAsync(BrowseFixture fixture)
    {
        using var response = await fixture.Client.GetAsync(
            $"{ApiRoutes.PlexPhotoController}/assets?plexLibraryId={fixture.Library.Id}&sort=sortIndex:asc",
            CancellationToken
        );
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(CancellationToken));
        var result = document.RootElement;
        result.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        result.GetProperty("errors").GetArrayLength().ShouldBe(1);
        result.GetProperty("successes").GetArrayLength().ShouldBe(0);
        result.TryGetProperty("value", out _).ShouldBeFalse();
    }

    private async Task AssertDisabledLibraryAsync(BrowseFixture fixture)
    {
        await fixture.Context.PlexLibraries
            .Where(x => x.Id == fixture.Library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        var response = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { PlexLibraryId = fixture.Library.Id });
        response.Response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Result.IsSuccess.ShouldBeFalse();
        response.Result.Errors.Count.ShouldBeGreaterThan(0);
    }

    private sealed record BrowseFixture(
        IReaparrDbContext Context,
        HttpClient Client,
        PlexLibrary Library,
        PlexPhotoFolder Root,
        IReadOnlyList<PlexPhotoAlbum> Albums,
        IReadOnlyList<PlexPhotoMediaData> Assets,
        PlexPhotoAlbum Album,
        PlexPhotoMediaData Asset,
        IReadOnlyList<int> AlbumAssetIds,
        string SearchTerm
    );

    [Test]
    public async Task ShouldKeepOfflineLinkedCatalogVisibleAndHideUnlinkedOrDisabledScopes()
    {
        // Arrange
        var seed = new Seed(29005);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (x, _) => x.SetupIdentityRequest(seed);
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 2;
                    x.PhotoAlbumCount = 1;
                    x.PhotoCount = 1;
                };
            }
        );
        var context = container.DbContext;
        var libraries = await context.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var serverId = libraries[0].PlexServerId;
        var allAssets = await context.PlexPhotoMediaData.CountAsync(CancellationToken);
        var firstAssets = await context.PlexPhotoMediaData
            .CountAsync(x => x.PlexLibraryId == libraries[0].Id, CancellationToken);
        using var client = container.GetApiClient();
        await client.SignIn();
        var fixture = new ScopeFixture(context, client, libraries, serverId, allAssets, firstAssets);

        // Act and Assert
        await AssertOfflineAndUnlinkedScopesAsync(fixture);
        await AssertDisabledScopesAsync(fixture);
    }

    private async Task AssertOfflineAndUnlinkedScopesAsync(ScopeFixture fixture)
    {
        await fixture.Context.PlexServerStatuses
            .Where(x => x.PlexServerId == fixture.ServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, false), CancellationToken);
        var offlineResponse = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { Page = 1, PageSize = 100 });
        offlineResponse.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        offlineResponse.Result.Value.ShouldNotBeNull();
        offlineResponse.Result.Value.TotalCount.ShouldBe(fixture.AllAssets);

        await fixture.Context.PlexAccountLibraries
            .Where(x => x.PlexLibraryId == fixture.Libraries[1].Id)
            .ExecuteDeleteAsync(CancellationToken);
        var unlinkedAggregate = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { Page = 1, PageSize = 100 });
        var unlinkedExplicit = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { PlexLibraryId = fixture.Libraries[1].Id });
        unlinkedAggregate.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        unlinkedAggregate.Result.Value.ShouldNotBeNull();
        unlinkedAggregate.Result.Value.TotalCount.ShouldBe(fixture.FirstAssets);
        unlinkedAggregate.Result.Value.Items.ShouldAllBe(
            x => x.PlexLibraryId == fixture.Libraries[0].Id
        );
        unlinkedExplicit.Response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task AssertDisabledScopesAsync(ScopeFixture fixture)
    {
        await fixture.Context.PlexLibraries
            .IgnoreIsEnabledFilter()
            .Where(x => x.Id == fixture.Libraries[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        var disabledLibraryAggregate = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { Page = 1, PageSize = 100 });
        var disabledLibraryExplicit = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { PlexLibraryId = fixture.Libraries[0].Id });
        disabledLibraryAggregate.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        disabledLibraryAggregate.Result.Value.ShouldNotBeNull();
        disabledLibraryAggregate.Result.Value.TotalCount.ShouldBe(0);
        disabledLibraryAggregate.Result.Value.TotalBytes.ShouldBe(0);
        disabledLibraryExplicit.Response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await fixture.Context.PlexLibraries
            .IgnoreIsEnabledFilter()
            .Where(x => x.Id == fixture.Libraries[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        await fixture.Context.PlexServers
            .IgnoreIsEnabledFilter()
            .Where(x => x.Id == fixture.ServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
        var disabledServerAggregate = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { Page = 1, PageSize = 100 });
        var disabledServerExplicit = await fixture.Client.GETAsync<
            GetPhotoAssetsEndpoint,
            GetPhotoAssetsRequest,
            ResultDTO<PhotoAssetPageDTO>
        >(new GetPhotoAssetsRequest { PlexLibraryId = fixture.Libraries[0].Id });
        disabledServerAggregate.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        disabledServerAggregate.Result.Value.ShouldNotBeNull();
        disabledServerAggregate.Result.Value.TotalCount.ShouldBe(0);
        disabledServerAggregate.Result.Value.TotalBytes.ShouldBe(0);
        disabledServerExplicit.Response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record ScopeFixture(
        IReaparrDbContext Context,
        HttpClient Client,
        IReadOnlyList<PlexLibrary> Libraries,
        int ServerId,
        int AllAssets,
        int FirstAssets
    );

    [Test]
    public async Task ShouldKeepRemovedExpansionRouteAbsent()
    {
        // Arrange
        var seed = new Seed(29004);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (x, _) => x.SetupIdentityRequest(seed);
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoAlbumCount = 1;
                    x.PhotoCount = 1;
                };
            }
        );
        var library = await container.DbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum, CancellationToken);
        using var client = container.GetApiClient();
        await client.SignIn();
        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync(
            $"{ApiRoutes.PlexLibraryController}/{library.Id}/photo-selections/expand",
            content,
            CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
