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
