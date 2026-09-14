using System.Globalization;
using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class TorznabFeedItemProjectionUnitTests
{
    [Test]
    public void ShouldKeepStableId_WhenRuntimeValuesChange()
    {
        // Arrange
        var projection = CreateProjection();
        var otherIntegration = new IntegrationIdentity(IntegrationType.Sonarr, Guid.NewGuid());

        // Act
        var first = projection.ToTorznabItem(
            new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()),
            "first-key",
            "http://first-host"
        );
        var second = projection.ToTorznabItem(otherIntegration, "second-key", "http://second-host");

        // Assert
        first.Guid.Value.ShouldBe(second.Guid.Value);
        first.Link.ShouldNotBe(second.Link);
        first.Guid.Value.ShouldStartWith("reaparr-");
        first.Guid.Value.Length.ShouldBe(72);
    }

    [Test]
    public void ShouldChangeStableId_WhenPlexIdentityChanges()
    {
        // Arrange
        var projection = CreateProjection();

        // Act
        var ids = new[]
        {
            projection.CreateStableId(),
            (
                projection with
                {
                    PlexServerMachineIdentifier = "444f1d4d82d020fa33821f80578c574f155cf14a",
                }
            ).CreateStableId(),
            (projection with { MediaType = PlexMediaType.Episode }).CreateStableId(),
            (projection with { PlexApiRatingKey = projection.PlexApiRatingKey + 1 }).CreateStableId(),
            (projection with { PlexApiMediaId = projection.PlexApiMediaId + 1 }).CreateStableId(),
            (projection with { PlexApiPartId = projection.PlexApiPartId + 1 }).CreateStableId(),
        };

        // Assert
        ids.Distinct().Count().ShouldBe(ids.Length);
    }

    [Test]
    public void ShouldKeepStableId_WhenCurrentCultureUsesNonLatinDigits()
    {
        // Arrange
        var projection = CreateProjection();
        var originalCulture = CultureInfo.CurrentCulture;

        // Act
        string invariantId;
        string alternateCultureId;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            invariantId = projection.CreateStableId();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            alternateCultureId = projection.CreateStableId();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        // Assert
        alternateCultureId.ShouldBe(invariantId);
    }

    [Test]
    public void ShouldEmitNumericParentAndSubcategoryAttributes()
    {
        // Arrange
        var projection = CreateProjection() with { GenreTypes = [PlexGenreType.Foreign] };

        // Act
        var item = projection.ToTorznabItem(
            new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()),
            "key",
            "http://localhost"
        );

        // Assert
        item.Attributes.Where(x => x.Name == "category").Select(x => x.Value).ShouldBe(["2000", "2040", "2010", "2070"]);
    }

    [Test]
    public void ShouldEmitAllMatchingTvGenreCategories()
    {
        // Arrange
        var projection = CreateProjection() with
        {
            MediaType = PlexMediaType.Episode,
            GenreTypes =
            [
                PlexGenreType.Foreign,
                PlexGenreType.Anime,
                PlexGenreType.Documentary,
                PlexGenreType.Sport,
            ],
        };

        // Act
        var item = projection.ToTorznabItem(
            new IntegrationIdentity(IntegrationType.Sonarr, Guid.NewGuid()),
            "key",
            "http://localhost"
        );

        // Assert
        item.Attributes.Where(x => x.Name == "category").Select(x => x.Value).ShouldBe([
            "5000", "5040", "5020", "5060", "5070", "5080",
        ]);
    }

    [Test]
    public void ShouldIgnoreUnknownAndGroupGenreTypes()
    {
        // Arrange
        var projection = CreateProjection() with
        {
            MediaType = PlexMediaType.Episode,
            GenreTypes = [PlexGenreType.Unknown, PlexGenreType.Group],
        };

        // Act
        var item = projection.ToTorznabItem(
            new IntegrationIdentity(IntegrationType.Sonarr, Guid.NewGuid()),
            "key",
            "http://localhost"
        );

        // Assert
        item.Attributes.Where(x => x.Name == "category").Select(x => x.Value).ShouldBe(["5000", "5040"]);
    }

    [Test]
    public void ShouldKeepMandatoryAttributes_WhenOptionalAttributesAreFiltered()
    {
        // Arrange
        var projection = CreateProjection();

        // Act
        var item = projection.ToTorznabItem(
            new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()),
            "key",
            "http://localhost",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "seeders" }
        );

        // Assert
        item.Attributes.ShouldContain(x => x.Name == "size");
        item.Attributes.ShouldContain(x => x.Name == "category");
        item.Attributes.ShouldContain(x => x.Name == "seeders");
        item.Attributes.ShouldNotContain(x => x.Name == "language");
    }

    [Test]
    [Arguments(VideoQuality.UHD_4K, ReleaseSource.DVD, "2000", "2030")]
    [Arguments(VideoQuality.SD, ReleaseSource.BluRay, "2000", "2030", "2050")]
    [Arguments(VideoQuality.UHD_4K, ReleaseSource.WebDl, "2000", "2045", "2070")]
    [Arguments(VideoQuality.FullHD, ReleaseSource.WebRip, "2000", "2040", "2070")]
    [Arguments(VideoQuality.FullHD, ReleaseSource.BluRayRemux, "2000", "2040", "2050")]
    public void ShouldApplyMovieQualityAndSourceCategoryPrecedence(
        VideoQuality resolution,
        ReleaseSource source,
        params string[] expectedCategories
    )
    {
        // Arrange
        var projection = CreateProjection() with { VideoResolution = resolution, Source = source };

        // Act
        var item = projection.ToTorznabItem(
            new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()),
            "key",
            "http://localhost"
        );

        // Assert
        item.Attributes.Where(x => x.Name == "category").Select(x => x.Value).ShouldBe(expectedCategories);
    }

    [Test]
    public void ShouldKeepStableId_WhenMutableReleaseMetadataChanges()
    {
        // Arrange
        var projection = CreateProjection();

        // Act
        var ids = new[]
        {
            projection.CreateStableId(),
            (projection with { DataId = 999 }).CreateStableId(),
            (projection with { Quality = VideoQuality.UHD_4K, Source = ReleaseSource.BluRay }).CreateStableId(),
            (projection with { Title = "Renamed.mkv", GenreTypes = [PlexGenreType.Anime] }).CreateStableId(),
        };

        // Assert
        ids.Distinct().ShouldHaveSingleItem();
    }

    [Test]
    public void ShouldKeepPublicationDate_WhenMutableReleaseMetadataChanges()
    {
        // Arrange
        var projection = CreateProjection();
        var changed = projection with
        {
            Quality = VideoQuality.UHD_4K,
            Source = ReleaseSource.BluRay,
            Title = "Renamed.mkv",
            GenreTypes = [PlexGenreType.Documentary],
        };

        // Act
        var first = projection.ToTorznabItem(new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()), "key", "http://localhost");
        var second = changed.ToTorznabItem(new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()), "key", "http://localhost");

        // Assert
        second.PubDate.ShouldBe(first.PubDate);
        second.PubDate.ShouldBe(projection.AddedAt.ToString("R"));
    }

    private static TorznabFeedItemProjection CreateProjection() =>
        new()
        {
            MediaType = PlexMediaType.Movie,
            MediaId = 1,
            DataId = 2,
            PlexServerId = 3,
            PlexServerMachineIdentifier = "333f1d4d82d020fa33821f80578c574f155cf14a",
            PlexLibraryId = 4,
            PlexApiRatingKey = 5,
            PlexApiMediaId = 6,
            PlexApiPartId = 7,
            Title = "Movie.2026.1080p",
            AddedAt = new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc),
            Size = 1024,
            Quality = VideoQuality.FullHD,
            VideoResolution = VideoQuality.FullHD,
            Source = ReleaseSource.WebDl,
            VideoCodec = "h264",
            AudioCodec = "aac",
            SeasonNumber = 0,
            EpisodeNumber = 0,
            TvdbId = 0,
            TmdbId = 0,
            ImdbId = string.Empty,
            GenreTypes = [],
        };
}
