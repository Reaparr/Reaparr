namespace Reaparr.Application.UnitTests;

public class GetAllMediaByTypeEndpointUnitTests
    : BaseEndpointUnitTest<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, PlexMediaStatisticsDTO>
{
    [Test]
    [Arguments(PlexMediaType.PhotoImage, null, null, nameof(GetAllMediaByTypeRequest.MediaType))]
    [Arguments(PlexMediaType.PhotoAlbum, null, 7, nameof(GetAllMediaByTypeRequest.QualityId))]
    [Arguments(PlexMediaType.MusicArtist, null, 7, nameof(GetAllMediaByTypeRequest.QualityId))]
    [Arguments(PlexMediaType.Movie, "unknown:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(PlexMediaType.Movie, "id:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(PlexMediaType.Movie, "title:asc,id:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(PlexMediaType.Movie, "quality:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(PlexMediaType.Movie, "sortTitle:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(PlexMediaType.Movie, "99:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(PlexMediaType.PhotoAlbum, "comparisonState:asc", null, nameof(GetAllMediaByTypeRequest.Sort))]
    [Arguments(
        PlexMediaType.OtherVideos,
        "title:asc,comparisonState:desc",
        null,
        nameof(GetAllMediaByTypeRequest.Sort)
    )]
    public async Task ShouldRejectInvalidRootRequestsWithoutDispatch(
        PlexMediaType mediaType,
        string? sort,
        int? qualityId,
        string expectedProperty
    )
    {
        // Arrange
        var request = new GetAllMediaByTypeRequest
        {
            MediaType = mediaType,
            Sort = sort,
            QualityId = qualityId,
        };

        // Act
        var result = await TestEndpointHandleAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationResult.ShouldNotBeNull().Errors.Select(x => x.PropertyName).ShouldBe([expectedProperty]);
        result.Response.ShouldBeNull();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMediaOverviewCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldDispatchPhotoAlbumRootUnchanged_WhenRequestingRootOverview()
    {
        // Arrange
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetMediaOverviewCommand>(c =>
                        c.Filter.MediaType == PlexMediaType.PhotoAlbum && c.Filter.PlexLibraryId == 17
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                Result.Ok(
                    new PagedMediaQueryResult
                    {
                        QueryHash = "photo-albums",
                        Page = 1,
                        PageSize = 20,
                        TotalCount = 2,
                        MediaCount = 2,
                    }
                )
            )
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(
            new GetAllMediaByTypeRequest { MediaType = PlexMediaType.PhotoAlbum, PlexLibraryId = 17 }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(200);
        result.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ResultDTO<PlexMediaStatisticsDTO>>(
            result.Endpoint.HttpContext.Response.Body,
            DefaultJsonSerializerOptions.ConfigStandard,
            CancellationToken
        );
        response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        (value.QueryHash, value.Page, value.PageSize, value.TotalCount, value.MediaCount).ShouldBe(
            ("photo-albums", 1, 20, 2, 2)
        );
        value.MediaList.ShouldBeEmpty();
        value.NavigationIndexes.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldProjectMediaQueryTelemetryWithoutChangingResponse()
    {
        // Arrange
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetMediaOverviewCommand>(command =>
                        command.Filter.MediaType == PlexMediaType.Movie
                        && command.Filter.PlexLibraryId == 17
                        && command.Filter.Parameters.Page == 2
                        && command.Filter.Parameters.PageSize == 30
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                Result.Ok(
                    new PagedMediaQueryResult
                    {
                        Page = 2,
                        PageSize = 30,
                        TotalCount = 2,
                        MediaCount = 2,
                        TotalMediaSize = 4096,
                    }
                )
            )
            .Verifiable(Times.Once());
        MediaQueryTelemetryDTO? telemetryRequest = null;
        string? telemetryFilter = null;
        Result<PlexMediaStatisticsDTO>? telemetryResult = null;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.MediaQueryFinished(
                    It.IsAny<MediaQueryTelemetryDTO>(),
                    It.IsAny<string?>(),
                    It.IsAny<Result<PlexMediaStatisticsDTO>>()
                )
            )
            .Callback<MediaQueryTelemetryDTO, string?, Result<PlexMediaStatisticsDTO>>(
                (request, filter, result) =>
                {
                    telemetryRequest = request;
                    telemetryFilter = filter;
                    telemetryResult = result;
                }
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 17,
                Page = 2,
                PageSize = 30,
                Search = "Alpha Beta",
                Sort = "title:desc",
            }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ResultDTO<PlexMediaStatisticsDTO>>(
            endpointResult.Endpoint.HttpContext.Response.Body,
            DefaultJsonSerializerOptions.ConfigStandard,
            CancellationToken
        );
        response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        (value.Page, value.PageSize, value.TotalCount, value.MediaCount, value.TotalMediaSize).ShouldBe(
            (2, 30, 2, 2, 4096L)
        );
        telemetryRequest.ShouldNotBeNull();
        telemetryRequest.MediaType.ShouldBe(PlexMediaType.Movie);
        telemetryRequest.PlexLibraryId.ShouldBe(17);
        telemetryRequest.Page.ShouldBe(2);
        telemetryRequest.PageSize.ShouldBe(30);
        telemetryRequest.Search.ShouldBe("Alpha Beta");
        telemetryRequest.Sort.ShouldBe("title:desc");
        telemetryFilter.ShouldBe("SearchTitle:contains:alpha&SearchTitle:contains:beta");
        telemetryResult.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        telemetryResult.Errors.Count.ShouldBe(0);
        telemetryResult.Value.TotalCount.ShouldBe(2);
        telemetryResult.Value.MediaCount.ShouldBe(2);
        telemetryResult.Value.TotalMediaSize.ShouldBe(4096);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "MediaDataList")]
    [Arguments(PlexMediaType.TvShow, "Qualities")]
    [Arguments(PlexMediaType.OtherVideos, "MediaDataList")]
    public async Task ShouldFilterTheCorrectVideoQualityCollection_WhenQualityIsSelected(
        PlexMediaType mediaType,
        string collection
    )
    {
        // Arrange
        var request = new GetAllMediaByTypeRequest
        {
            MediaType = mediaType,
            PlexLibraryId = 17,
            QualityId = 7,
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetMediaOverviewCommand>(command =>
                        command.Filter.MediaType == mediaType
                        && command.Filter.PlexLibraryId == 17
                        && command.Filter.Parameters.Filter == $"{collection}:any:VideoQuality:eq:FullHD"
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new PagedMediaQueryResult()))
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(1, null, null, null, nameof(GetAllMediaByTypeRequest.CountryId))]
    [Arguments(null, 1, null, null, nameof(GetAllMediaByTypeRequest.RoleId))]
    [Arguments(null, null, 1, null, nameof(GetAllMediaByTypeRequest.GenreId))]
    public void ShouldRejectUnsupportedPhotoAlbumFilters_WhenValidatingRequest(
        int? countryId,
        int? roleId,
        int? genreId,
        int? qualityId,
        string propertyName
    )
    {
        var result = new GetAllMediaByTypeRequestValidator().Validate(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.PhotoAlbum,
                CountryId = countryId,
                RoleId = roleId,
                GenreId = genreId,
                QualityId = qualityId,
            }
        );

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(x => x.PropertyName).ShouldBe([propertyName]);
    }

    [Test]
    public void ShouldRejectNonPositiveFriendlyFilterIdsButAllowAllMediaComparisonState_WhenValidatingRequest()
    {
        var validator = new GetAllMediaByTypeRequestValidator();
        var request = new GetAllMediaByTypeRequest
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = 0,
            CountryId = 0,
            GenreId = -1,
            RoleId = 0,
            QualityId = -1,
            ComparisonState = PlexMediaComparisonState.Missing,
        };
        var result = validator.Validate(request);
        result.IsValid.ShouldBeFalse();
        result
            .Errors.Select(x => x.PropertyName)
            .ShouldBe(
                new[]
                {
                    nameof(GetAllMediaByTypeRequest.CountryId),
                    nameof(GetAllMediaByTypeRequest.GenreId),
                    nameof(GetAllMediaByTypeRequest.RoleId),
                    nameof(GetAllMediaByTypeRequest.QualityId),
                }
            );
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public void ShouldRejectComparisonForNonComparableRoots_WhenValidatingRequest(PlexMediaType mediaType)
    {
        var validator = new GetAllMediaByTypeRequestValidator();
        var request = new GetAllMediaByTypeRequest
        {
            MediaType = mediaType,
            ComparisonState = PlexMediaComparisonState.Missing,
        };

        var result = validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(x => x.PropertyName).ShouldBe(new[] { nameof(GetAllMediaByTypeRequest.ComparisonState) });
    }
}
