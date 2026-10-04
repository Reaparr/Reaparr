namespace Reaparr.Application.UnitTests;

public class GetAllMediaByTypeEndpointUnitTests
    : BaseEndpointUnitTest<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, PlexMediaStatisticsDTO>
{
    [Test]
    public async Task ShouldRejectLeafPhotosWithoutDispatch_WhenRequestingRootOverview()
    {
        // Arrange
        var request = new GetAllMediaByTypeRequest { MediaType = PlexMediaType.PhotoImage };

        // Act
        var result = await TestEndpointHandleAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result
            .ValidationResult.ShouldNotBeNull()
            .Errors.Select(x => x.PropertyName)
            .ShouldBe([nameof(GetAllMediaByTypeRequest.MediaType)]);
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
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public void ShouldRejectComparisonForNewRoots_WhenValidatingRequest(PlexMediaType mediaType)
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
