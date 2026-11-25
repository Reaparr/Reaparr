using Quartz;

namespace Reaparr.Application.UnitTests;

public class ApplyRemoteMovieComparisonStateCommandUnitTests
    : BaseCommandUnitTest<ApplyRemoteMovieComparisonStateCommand>
{
    [Test]
    public async Task ShouldReturnSuccess_WhenItemsAreEmpty()
    {
        // Arrange
        var command = new ApplyRemoteMovieComparisonStateCommand([], 1);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldMarkOwned_WhenItemMatchesAnyCurrentOwnedLibrary()
    {
        // Arrange
        await SetupDatabase(41, config =>
        {
            config.PlexServerCount = 3;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var matchedOwnedLibrary = libraries[1];
        var missingOwnedLibrary = libraries[2];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(matchedOwnedLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(missingOwnedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 21, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(matchedOwnedLibrary.Id, new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(missingOwnedLibrary.Id, new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        matchedOwnedLibrary = await GetLibraryAsync(matchedOwnedLibrary.Id);
        missingOwnedLibrary = await GetLibraryAsync(missingOwnedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(matchedOwnedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, matchedOwnedLibrary);
        await AddCurrentScopeAsync(remoteLibrary, missingOwnedLibrary);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                matchedOwnedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Owned.ToComparisonId());
    }

    [Test]
    public async Task ShouldMarkHigherQuality_WhenAnyCurrentOwnedLibraryHitIsHigherQuality()
    {
        // Arrange
        await SetupDatabase(42, config =>
        {
            config.PlexServerCount = 3;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var matchedOwnedLibrary = libraries[1];
        var higherQualityOwnedLibrary = libraries[2];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(matchedOwnedLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(higherQualityOwnedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 21, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(matchedOwnedLibrary.Id, new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(
            higherQualityOwnedLibrary.Id,
            new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc)
        );
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        matchedOwnedLibrary = await GetLibraryAsync(matchedOwnedLibrary.Id);
        higherQualityOwnedLibrary = await GetLibraryAsync(higherQualityOwnedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var matchedOwnedMovie = await GetLibraryMovieAsync(matchedOwnedLibrary.Id);
        var higherQualityOwnedMovie = await GetLibraryMovieAsync(higherQualityOwnedLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, matchedOwnedLibrary);
        await AddCurrentScopeAsync(remoteLibrary, higherQualityOwnedLibrary);
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                matchedOwnedLibrary.Id,
                remoteMovie.Id,
                matchedOwnedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                higherQualityOwnedLibrary.Id,
                remoteMovie.Id,
                higherQualityOwnedMovie.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
    }

    [Test]
    public async Task ShouldMarkMissing_WhenItemHasNoHitsInAnyCurrentOwnedLibrary()
    {
        // Arrange
        await SetupDatabase(43, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 21, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Missing.ToComparisonId());
    }

    [Test]
    public async Task ShouldUseCompletedScope_WhenLibraryMetadataChangedAndNoComparisonIsQueued()
    {
        // Arrange
        await SetupDatabase(44, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 14, 8, 33, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        var ownedMovie = await GetLibraryMovieAsync(ownedLibrary.Id);
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)));
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([], false)));

        dbContext.PlexComparisonScopes.Add(
            new PlexComparisonState
            {
                Id = 0,
                RemotePlexLibraryId = remoteLibrary.Id,
                OwnedPlexLibraryId = ownedLibrary.Id,
                MediaType = PlexMediaType.Movie,
                CompletedAt = DateTime.UtcNow,
            }
        );
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
    }

    [Test]
    public async Task ShouldUseCompletedScope_WhenLibraryMetadataChangedAndComparisonIsQueued()
    {
        // Arrange
        await SetupDatabase(45, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 22, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 22, 14, 8, 33, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        dbContext.PlexComparisonScopes.Add(
            new PlexComparisonState
            {
                Id = 0,
                RemotePlexLibraryId = remoteLibrary.Id,
                OwnedPlexLibraryId = ownedLibrary.Id,
                MediaType = PlexMediaType.Movie,
                CompletedAt = DateTime.UtcNow,
            }
        );
        var comparisonKey = PlexLibraryComparisonJob.GetJobKey(ownedLibrary.Id, remoteLibrary.Id);
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)));
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(
                    new PagedResult<JobHeader>(
                        [
                            new JobHeader(
                                comparisonKey,
                                null,
                                typeof(PlexLibraryComparisonJob).FullName!,
                                false,
                                true,
                                false,
                                true
                            ),
                        ],
                        false
                    )
                )
            );
        await dbContext.SaveChangesAsync(CancellationToken);

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Missing.ToComparisonId());
    }

    [Test]
    public async Task ShouldIgnoreFailedQueue_WhenNoCurrentScopeExists()
    {
        // Arrange
        await SetupDatabase(47, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)));
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([], false)));

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
    }

    [Test]
    public async Task ShouldIgnoreCompletedQueue_WhenNoCurrentScopeExists()
    {
        // Arrange
        await SetupDatabase(46, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
            config.MovieCount = 1;
        });

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovie = await GetLibraryMovieAsync(remoteLibrary.Id);
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns("test-scheduler");
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)));
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([], false)));

        var items = new List<PlexMediaSlimDTO> { CreateMovieItem(remoteMovie) };

        // Act
        var result = await TestHandlerExecuteAsync(new ApplyRemoteMovieComparisonStateCommand(items, remoteLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
    }
}
