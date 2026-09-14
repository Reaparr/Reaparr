namespace Reaparr.Data.UnitTests.Configurations;

public class MediaOverviewSnapshotConfigurationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldConfigureUniqueMovieSnapshotPerCanonicalMedia()
    {
        // Arrange
        await SetupDatabase(
            81001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;

        // Act
        var entityType = context.Model.FindEntityType(typeof(MediaOverviewMovieSnapshot)).ShouldNotBeNull();
        var uniqueIndexes = entityType.GetIndexes().Where(x => x.IsUnique).ToList();

        // Assert
        uniqueIndexes.ShouldContain(x =>
            x.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(MediaOverviewMovieSnapshot.PlexMovieId) })
        );
    }

    [Test]
    public async Task ShouldConfigureUniqueTvShowSnapshotPerCanonicalMedia()
    {
        // Arrange
        await SetupDatabase(
            81002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;

        // Act
        var entityType = context.Model.FindEntityType(typeof(MediaOverviewTvShowSnapshot)).ShouldNotBeNull();
        var uniqueIndexes = entityType.GetIndexes().Where(x => x.IsUnique).ToList();

        // Assert
        uniqueIndexes.ShouldContain(x =>
            x.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(MediaOverviewTvShowSnapshot.PlexTvShowId) })
        );
    }

    [Test]
    public async Task ShouldCascadeMovieSnapshotDeleteAndLeaveUnrelatedRowsIntact()
    {
        // Arrange
        await SetupDatabase(
            81003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;
        var movieIds = await context.PlexMovies.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        context.MediaOverviewMovieSnapshots.AddRange(
            movieIds.Select(
                (movieId, index) =>
                    new MediaOverviewMovieSnapshot
                    {
                        PlexMovieId = movieId,
                        PlexLibraryId = 1,
                        TitleRank = index,
                        YearRank = index,
                        AddedAtRank = index,
                        UpdatedAtRank = index,
                        DurationRank = index,
                        MediaSizeRank = index,
                        QualityRank = index,
                    }
            )
        );
        await context.SaveChangesAsync(CancellationToken);

        // Act
        await context.PlexMovies.Where(x => x.Id == movieIds[0]).ExecuteDeleteAsync(CancellationToken);

        // Assert
        (await context.MediaOverviewMovieSnapshots.CountAsync(CancellationToken)).ShouldBe(1);
        (await context.MediaOverviewMovieSnapshots.SingleAsync(CancellationToken)).PlexMovieId.ShouldBe(movieIds[1]);
    }

    [Test]
    public async Task ShouldCascadeTvShowSnapshotDeleteAndLeaveUnrelatedRowsIntact()
    {
        // Arrange
        await SetupDatabase(
            81004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
            }
        );
        await using var context = (ReaparrDbContext)IDbContext;
        var tvShowIds = await context.PlexTvShows.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        context.MediaOverviewTvShowSnapshots.AddRange(
            tvShowIds.Select(
                (tvShowId, index) =>
                    new MediaOverviewTvShowSnapshot
                    {
                        PlexTvShowId = tvShowId,
                        PlexLibraryId = 1,
                        TitleRank = index,
                        YearRank = index,
                        AddedAtRank = index,
                        UpdatedAtRank = index,
                        DurationRank = index,
                        MediaSizeRank = index,
                        QualityRank = index,
                    }
            )
        );
        await context.SaveChangesAsync(CancellationToken);

        // Act
        await context.PlexTvShows.Where(x => x.Id == tvShowIds[0]).ExecuteDeleteAsync(CancellationToken);

        // Assert
        (await context.MediaOverviewTvShowSnapshots.CountAsync(CancellationToken)).ShouldBe(1);
        (await context.MediaOverviewTvShowSnapshots.SingleAsync(CancellationToken)).PlexTvShowId.ShouldBe(tvShowIds[1]);
    }
}
