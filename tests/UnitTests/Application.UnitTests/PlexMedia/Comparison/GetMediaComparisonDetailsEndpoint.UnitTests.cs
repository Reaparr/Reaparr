namespace Reaparr.Application.UnitTests;

public class GetMediaComparisonDetailsEndpointUnitTests
    : BaseEndpointUnitTest<
        GetMediaComparisonDetailsEndpoint,
        GetMediaComparisonDetailsEndpointRequest,
        ResultDTO<PlexMediaComparisonDetailsDTO>
    >
{
    [Test]
    public async Task ShouldDispatchMovieCommand_WhenMovieComparisonDetailsRequested()
    {
        // Arrange
        var request = new GetMediaComparisonDetailsEndpointRequest(1887, PlexMediaType.Movie);
        var commandResult = Result.Ok(
            new PlexMediaComparisonDetailsDTO
            {
                PlexMediaId = request.PlexMediaId,
                Type = PlexMediaType.Movie,
                State = PlexMediaComparisonState.Owned,
                Rows = [],
            }
        );

        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetMovieMediaComparisonDetailsCommand) != null
                && ((GetMovieMediaComparisonDetailsCommand)x).PlexMediaId == request.PlexMediaId
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetTvShowMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Never);

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Type.ShouldBe(PlexMediaType.Movie);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Episode)]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldReturnFailure_WhenUnsupportedMediaTypeReachesHandler(PlexMediaType type)
    {
        // Arrange
        var request = new GetMediaComparisonDetailsEndpointRequest(1889, type);
        var endpoint = SetupEndpointUnitTest<GetMediaComparisonDetailsEndpoint>();
        endpoint.HttpContext.Response.Body = new MemoryStream();

        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetMovieMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(Result.Fail<PlexMediaComparisonDetailsDTO>("Unexpected dispatch"))
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetTvShowMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(Result.Fail<PlexMediaComparisonDetailsDTO>("Unexpected dispatch"))
            .Verifiable(Times.Never);

        // Act
        await endpoint.HandleAsync(request, CancellationToken);
        var result = await GetEndpointResponseAsync(endpoint, CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Select(x => x.Message).ShouldBe(["Unsupported media type"]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldRejectRequest_WhenPlexMediaIdIsNotPositive()
    {
        // Arrange
        var request = new GetMediaComparisonDetailsEndpointRequest(0, PlexMediaType.Movie);

        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetMovieMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(Result.Fail<PlexMediaComparisonDetailsDTO>("Unexpected dispatch"))
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetTvShowMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(Result.Fail<PlexMediaComparisonDetailsDTO>("Unexpected dispatch"))
            .Verifiable(Times.Never);

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.ShouldNotBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Episode)]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldRejectRequest_WhenMediaTypeIsUnsupportedByValidator(PlexMediaType type)
    {
        // Arrange
        var request = new GetMediaComparisonDetailsEndpointRequest(1889, type);

        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetMovieMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(Result.Fail<PlexMediaComparisonDetailsDTO>("Unexpected dispatch"))
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetTvShowMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(Result.Fail<PlexMediaComparisonDetailsDTO>("Unexpected dispatch"))
            .Verifiable(Times.Never);

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.HasValidator.ShouldBeTrue();
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.Select(x => x.PropertyName)
            .ShouldBe([nameof(GetMediaComparisonDetailsEndpointRequest.Type)]);
        endpointResult.Response.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchTvShowCommand_WhenTvShowComparisonDetailsRequested()
    {
        // Arrange
        var request = new GetMediaComparisonDetailsEndpointRequest(1888, PlexMediaType.TvShow);
        var commandResult = Result.Ok(
            new PlexMediaComparisonDetailsDTO
            {
                PlexMediaId = request.PlexMediaId,
                Type = PlexMediaType.TvShow,
                State = PlexMediaComparisonState.Owned,
                Rows = [],
            }
        );

        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetMovieMediaComparisonDetailsCommand) != null
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result<PlexMediaComparisonDetailsDTO>>(x =>
                (x as GetTvShowMediaComparisonDetailsCommand) != null
                && ((GetTvShowMediaComparisonDetailsCommand)x).PlexMediaId == request.PlexMediaId
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once);

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Type.ShouldBe(PlexMediaType.TvShow);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
