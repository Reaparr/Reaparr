using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewCommand>
{
    [Test]
    public async Task ShouldDispatchMovieCommandOnly_WhenMovieRequested()
    {
        // Arrange
        var filter = CreateFilter(PlexMediaType.Movie);
        var expected = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = 2,
            PageSize = 25,
            TotalCount = 7,
        };
        Mock.SetupCommand<Result<PagedMediaQueryResult>>(command =>
                command is GetMediaOverviewMovieCommand && ((GetMediaOverviewMovieCommand)command).Filter == filter
            )
            .ReturnsAsync(Result.Ok(expected))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeSameAs(expected);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMediaOverviewTvShowCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldDispatchTvShowCommandOnly_WhenTvShowRequested()
    {
        // Arrange
        var filter = CreateFilter(PlexMediaType.TvShow);
        var expected = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = 2,
            PageSize = 25,
            TotalCount = 9,
        };
        Mock.SetupCommand<Result<PagedMediaQueryResult>>(command =>
                command is GetMediaOverviewTvShowCommand && ((GetMediaOverviewTvShowCommand)command).Filter == filter
            )
            .ReturnsAsync(Result.Ok(expected))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeSameAs(expected);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMediaOverviewMovieCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldRejectUnsupportedMediaType_WithoutDispatching()
    {
        // Arrange
        var command = new GetMediaOverviewCommand(CreateFilter(PlexMediaType.Music));

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMediaOverviewMovieCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMediaOverviewTvShowCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldRejectInvalidPaging_WithoutDispatching()
    {
        // Arrange
        var filter = CreateFilter(PlexMediaType.Movie) with
        {
            Parameters = new FlexQueryParameters { Page = 0, PageSize = 101 },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewCommand(filter));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetMediaOverviewMovieCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static MediaQueryFilter CreateFilter(PlexMediaType mediaType) =>
        new()
        {
            MediaType = mediaType,
            PlexLibraryId = 0,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 2,
                PageSize = 25,
                Sort = "year:desc",
            },
        };
}
