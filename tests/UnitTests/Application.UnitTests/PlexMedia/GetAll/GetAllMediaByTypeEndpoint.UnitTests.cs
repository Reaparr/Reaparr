

namespace Reaparr.Application.UnitTests;

public class GetAllMediaByTypeEndpointUnitTests
    : BaseEndpointUnitTest<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, PlexMediaStatisticsDTO>
{
    [Test]
    public async Task ShouldMapFriendlyRequestToMediaOverviewQuery_WhenHandlingRequest()
    {
        await SetupDatabase(42, config => { config.PlexServerCount = 1; config.PlexMovieLibraryCount = 1; });
        var libraryId = await IDbContext.PlexLibraries.Where(x => x.Type == PlexMediaType.Movie).Select(x => x.Id).FirstAsync(CancellationToken);
        var request = new GetAllMediaByTypeRequest
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = libraryId,
            Page = 2,
            PageSize = 25,
            Search = "matrix",
            CountryId = 7,
            GenreId = 13,
            RoleId = 11,
            QualityId = 480,
            ComparisonState = PlexMediaComparisonState.Missing,
            Sort = "sortIndex:asc",
            FilterOwnedMedia = true,
            FilterOfflineMedia = true,
        };
        Mock.SetupCommand<Result<PagedMediaQueryResult>>(
                command =>
                    ((GetMediaOverviewCommand)command).Filter.MediaType == PlexMediaType.Movie
                    && ((GetMediaOverviewCommand)command).Filter.PlexLibraryId == libraryId
                    && ((GetMediaOverviewCommand)command).Filter.Parameters.Page == 2
                    && ((GetMediaOverviewCommand)command).Filter.Parameters.PageSize == 25
                    && ((GetMediaOverviewCommand)command).Filter.Parameters.Filter
                        == "SearchTitle:contains:matrix&Countries:any:Id:eq:7&Actors:any:Id:eq:11&Genres:any:Id:eq:13&MediaDataList:any:Quality:eq:SD"
                    && ((GetMediaOverviewCommand)command).Filter.ComparisonState == PlexMediaComparisonState.Missing
                    && ((GetMediaOverviewCommand)command).Filter.Parameters.Sort == "sortIndex:asc"
                    && ((GetMediaOverviewCommand)command).Filter.FilterOwnedMedia
                    && ((GetMediaOverviewCommand)command).Filter.FilterOfflineMedia
            )
            .ReturnsAsync(Result.Ok(new PagedMediaQueryResult()))
            .Verifiable(Times.Once());

        // Act
        await TestEndpointHandleAsync(request);

        // Assert
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
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GetAllMediaByTypeRequest.CountryId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GetAllMediaByTypeRequest.GenreId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GetAllMediaByTypeRequest.RoleId));
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GetAllMediaByTypeRequest.QualityId));
        result.Errors.ShouldNotContain(x => x.PropertyName == nameof(GetAllMediaByTypeRequest.PlexLibraryId));
    }
}
