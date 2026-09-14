using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Reaparr.Data.UnitTests.Configurations;

public class TorznabFeedOrderingIndexConfigurationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldConfigureNewestFirstTorznabFeedIndexes()
    {
        // Arrange
        await SetupDatabase(626560);
        await using var context = (ReaparrDbContext)IDbContext;
        var model = context.GetService<IDesignTimeModel>().Model;

        // Act
        var movieIndex = FindFeedIndex<PlexMovie>(model);
        var episodeIndex = FindFeedIndex<PlexTvShowEpisode>(model);

        // Assert
        movieIndex.IsDescending.ShouldBeEmpty();
        episodeIndex.IsDescending.ShouldBeEmpty();
    }

    private static IIndex FindFeedIndex<T>(IModel model)
    {
        var entityType = model.FindEntityType(typeof(T)).ShouldNotBeNull();
        return entityType
            .GetIndexes()
            .Single(x =>
                x.Properties.Select(property => property.Name)
                    .SequenceEqual(
                        [
                            nameof(BasePlexMedia.AddedAt),
                            nameof(BasePlexMedia.PlexServerId),
                            nameof(BasePlexMedia.PlexApiRatingKey),
                            nameof(BasePlexMedia.Id),
                        ]
                    )
            );
    }
}
