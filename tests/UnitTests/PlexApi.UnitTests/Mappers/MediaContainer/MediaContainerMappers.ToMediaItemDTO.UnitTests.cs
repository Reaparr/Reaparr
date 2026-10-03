using LukeHagar.PlexAPI.SDK.Models.Components;
using NodaTime;
using Guids = LukeHagar.PlexAPI.SDK.Models.Components.Guids;
using Media = LukeHagar.PlexAPI.SDK.Models.Components.Media;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;
using Part = LukeHagar.PlexAPI.SDK.Models.Components.Part;
using Stream = LukeHagar.PlexAPI.SDK.Models.Components.Stream;
using Tag = LukeHagar.PlexAPI.SDK.Models.Components.Tag;

namespace Reaparr.PlexApi.UnitTests
{
    public class MediaContainerMappersUnitTests : BaseUnitTest
    {
        [Test]
        public void ShouldMapAllPropertiesCorrectly_WhenApiResponseHasMovieValues()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var addedAtUnixTime = now.ToUnixTimeSeconds();
            var updatedAtUnixTime = now.AddDays(1).ToUnixTimeSeconds();

            var sourceData = new Metadata
            {
                RatingKey = "123",
                Key = "/library/metadata/123",
                Type = "movie",
                Title = "Test Movie",
                Summary = "Test summary",
                Year = 2023,
                OriginalTitle = "Original Test Movie",
                ChildCount = 5,
                Studio = "Test Studio",
                ContentRating = "PG-13",
                Duration = 5400000, // 90 minutes in milliseconds
                Thumb = "/thumb/path",
                Art = "/art/path",
                Theme = "/theme/path",
                Guid = "plex://movie/guid",
                AddedAt = addedAtUnixTime,
                UpdatedAt = updatedAtUnixTime,
                OriginallyAvailableAt = LocalDate.FromDateOnly(new DateOnly(2023, 01, 01)),
                AudienceRating = 8.5f,
                Rating = 9.0f,
                GrandparentTitle = "Series Title",
                ParentTitle = "Season 1",
                ParentGuid = "plex://season/guid",
                ParentRatingKey = "456",

                // Collections of nested objects
                Media =
                [
                    new Media
                    {
                        Id = 789,
                        Duration = 5400000,
                        Bitrate = 5000,
                        Width = 1920,
                        Height = 1080,
                        AspectRatio = 1.78f,
                        AudioChannels = 6,
                        AudioCodec = "aac",
                        VideoCodec = "h264",
                        VideoResolution = "1080p",
                        Container = "mp4",
                        VideoFrameRate = "24p",
                        VideoProfile = "high",
                        AudioProfile = "lc",
                        HasVoiceActivity = HasVoiceActivity.CreateBoolean(true),
                        OptimizedForStreaming = OptimizedForStreaming.One,
                        Part =
                        [
                            new Part
                            {
                                Id = 101,
                                Accessible = true,
                                Exists = true,
                                Key = "/library/parts/101",
                                Indexes = "sd",
                                Duration = 5400000,
                                File = "/movies/testmovie.mp4",
                                Size = 5000000000,
                                Container = "mp4",
                                VideoProfile = "high",
                                AudioProfile = "lc",
                                Stream =
                                [
                                    new Stream
                                    {
                                        Default = true,
                                        Codec = "h264",
                                        Index = 0,
                                        Bitrate = 4000,
                                        Language = "English",
                                        LanguageTag = "en",
                                        LanguageCode = "eng",
                                        Selected = true,
                                    },
                                ],
                            },
                        ],
                    },
                ],
                Genre = [new Tag { TagValue = "Action" }, new Tag { TagValue = "Sci-Fi" }],
                Country = [new Tag { TagValue = "USA" }],
                Role = [new Tag { TagValue = "Actor Name" }],
                Guids = [new Guids { Id = "imdb://tt1234567" }],
            };

            // Act
            var result = sourceData.ToMediaItemDTO();

            // Assert
            result.ShouldNotBeNull();
            result.RatingKey.ShouldBe(int.Parse(sourceData.RatingKey));
            result.Key.ShouldBe(sourceData.Key);
            result.Title.ShouldBe(sourceData.Title);
            result.Summary.ShouldBe(sourceData.Summary);
            result.Year.ShouldBe(sourceData.Year ?? 0);
            result.OriginalTitle.ShouldBe(sourceData.OriginalTitle);
            result.ChildCount.ShouldBe(sourceData.ChildCount ?? 0);
            result.Studio.ShouldBe(sourceData.Studio);
            result.ContentRating.ShouldBe(sourceData.ContentRating);

            // Duration is in milliseconds and we want seconds
            result.Duration.ShouldBe((sourceData.Duration ?? 0) / 1000);

            result.Thumb.ShouldBe(sourceData.Thumb);
            result.Art.ShouldBe(sourceData.Art);
            result.Theme.ShouldBe(sourceData.Theme);
            result.Guid.ShouldBe(sourceData.Guid);

            // DateTime conversions
            result.AddedAt.ShouldBe(
                DateTimeExtensions.FromUnixTime(sourceData.AddedAt),
                tolerance: TimeSpan.FromSeconds(1)
            );
            result.UpdatedAt.ShouldBe(DateTimeExtensions.FromUnixTime(sourceData.UpdatedAt ?? 0));

            result.OriginallyAvailableAt.ShouldNotBeNull();
            result.OriginallyAvailableAt.ShouldBe(sourceData.OriginallyAvailableAt.ToString());

            result.GrandparentTitle.ShouldBe(sourceData.GrandparentTitle);
            result.ParentTitle.ShouldBe(sourceData.ParentTitle);
            result.ParentGuid.ShouldBe(sourceData.ParentGuid);
            result.ParentRatingKey.ShouldBe(sourceData.ParentRatingKey);
            result.AudienceRating.ShouldBe(sourceData.AudienceRating ?? 0f);
            result.Rating.ShouldBe(sourceData.Rating ?? 0f);

            // Collections
            result.Genre.Count.ShouldBe(sourceData.Genre.Count);
            result.Genre.First().Name.ShouldBe(sourceData.Genre.First().TagValue);

            result.Country.Count.ShouldBe(sourceData.Country.Count);
            result.Country.First().Name.ShouldBe(sourceData.Country.First().TagValue);

            result.Role.Count.ShouldBe(sourceData.Role.Count);
            result.Role.First().Name.ShouldBe(sourceData.Role.First().TagValue);

            result.Guids.Count.ShouldBe(sourceData.Guids.Count);
            result.Guids.First().Id.ShouldBe(sourceData.Guids.First().Id);

            // Media
            result.Media.Count.ShouldBe(1);
            var media = result.Media.First();
            var sourceMedia = sourceData.Media.First();

            media.Id.ShouldBe(Convert.ToInt32(sourceMedia.Id));
            media.Duration.ShouldBe(sourceMedia.Duration!.Value);
            media.Bitrate.ShouldBe(sourceMedia.Bitrate!.Value);
            media.Width.ShouldBe(sourceMedia.Width!.Value);
            media.Height.ShouldBe(sourceMedia.Height!.Value);
            media.OptimizedForStreaming.ShouldBeTrue();

            // Parts
            media.Parts.Count.ShouldBe(1);
            var part = media.Parts.First();
            var sourcePart = sourceMedia.Part!.First();

            part.Id.ShouldBe(Convert.ToInt32(sourcePart.Id));
            part.Key.ShouldBe(sourcePart.Key);
            part.File.ShouldBe(sourcePart.File);

            // Streams
            part.Stream.Count.ShouldBe(1);
            var stream = part.Stream.First();
            var sourceStream = sourcePart.Stream!.First();

            stream.Id.ShouldBe(sourceStream.Id);
            stream.Codec.ShouldBe(sourceStream.Codec);
            stream.Language.ShouldBe(sourceStream.Language);
        }

        [Test]
        public void ShouldMapMissingIndexesToMinusOne_WhenApiResponseOmitsIndexes()
        {
            // Arrange
            var sourceData = new Metadata
            {
                RatingKey = "456",
                Key = "/library/metadata/456",
                Type = "episode",
                Title = "Unknown episode",
            };

            // Act
            var result = sourceData.ToMediaItemDTO();

            // Assert
            result.Index.ShouldBe(-1);
            result.ParentIndex.ShouldBe(-1);
        }

        [Test]
        public void ShouldPreserveZeroIndexes_WhenApiResponseContainsZeroIndexes()
        {
            // Arrange
            var sourceData = new Metadata
            {
                RatingKey = "456",
                Key = "/library/metadata/456",
                Type = "episode",
                Title = "Special",
                Index = 0,
                ParentIndex = 0,
            };

            // Act
            var result = sourceData.ToMediaItemDTO();

            // Assert
            result.Index.ShouldBe(0);
            result.ParentIndex.ShouldBe(0);
        }

        [Test]
        public void ShouldMapAllPropertiesCorrectly_WhenApiResponseHasTvShowValues()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var addedAtUnixTime = now.ToUnixTimeSeconds();
            var updatedAtUnixTime = now.AddDays(1).ToUnixTimeSeconds();

            var sourceData = new Metadata
            {
                RatingKey = "456",
                Key = "/library/metadata/456",
                Type = "show",
                Title = "Test TV Show",
                Summary = "Test TV show summary",
                Year = 2020,
                OriginalTitle = "Original Test TV Show",
                ChildCount = 8, // Seasons count
                Studio = "Test TV Studio",
                ContentRating = "TV-MA",
                Thumb = "/tv/thumb/path",
                Art = "/tv/art/path",
                Theme = "/tv/theme/path",
                Guid = "plex://show/guid",
                AddedAt = addedAtUnixTime,
                UpdatedAt = updatedAtUnixTime,
                OriginallyAvailableAt = LocalDate.FromDateOnly(new DateOnly(2020, 05, 15)),
                AudienceRating = 9.1f,
                Rating = 9.3f,
                LeafCount = 42, // Episode count
                ViewedLeafCount = 12, // Viewed episodes

                // Collections of nested objects
                Media = [], // TV Shows typically don't have direct media, episodes do
                Genre = [new Tag { TagValue = "Drama" }, new Tag { TagValue = "Mystery" }],
                Country = [new Tag { TagValue = "United Kingdom" }],
                Role = [new Tag { TagValue = "Lead Actor" }, new Tag { TagValue = "Supporting Actor" }],
                Guids = [new Guids { Id = "imdb://tt7654321" }, new Guids { Id = "tmdb://12345" }],
            };

            // Act
            var result = sourceData.ToMediaItemDTO();

            // Assert
            result.ShouldNotBeNull();
            result.RatingKey.ShouldBe(int.Parse(sourceData.RatingKey));
            result.Key.ShouldBe(sourceData.Key);
            result.Title.ShouldBe(sourceData.Title);
            result.Summary.ShouldBe(sourceData.Summary);
            result.Year.ShouldBe(sourceData.Year ?? 0);
            result.OriginalTitle.ShouldBe(sourceData.OriginalTitle);
            result.ChildCount.ShouldBe(sourceData.ChildCount ?? 0);
            result.Studio.ShouldBe(sourceData.Studio);
            result.ContentRating.ShouldBe(sourceData.ContentRating);

            // Duration is in milliseconds and we want seconds, but TV shows typically don't have duration at the show level
            result.Duration.ShouldBe(0);

            result.Thumb.ShouldBe(sourceData.Thumb);
            result.Art.ShouldBe(sourceData.Art);
            result.Theme.ShouldBe(sourceData.Theme);
            result.Guid.ShouldBe(sourceData.Guid);

            // DateTime conversions
            result.AddedAt.ShouldBe(
                DateTimeExtensions.FromUnixTime(sourceData.AddedAt),
                tolerance: TimeSpan.FromSeconds(1)
            );
            result.UpdatedAt.ShouldBe(DateTimeExtensions.FromUnixTime(sourceData.UpdatedAt ?? 0));

            // Parse the string date to NodaTime.LocalDate if not null
            result.OriginallyAvailableAt.ShouldNotBeNull();
            result.OriginallyAvailableAt.ShouldBe(sourceData.OriginallyAvailableAt.ToString());

            result.AudienceRating.ShouldBe(sourceData.AudienceRating ?? 0f);
            result.Rating.ShouldBe(sourceData.Rating ?? 0f);

            // Collections
            result.Genre.Count.ShouldBe(sourceData.Genre.Count);
            result.Genre.First().Name.ShouldBe(sourceData.Genre.First().TagValue);
            result.Genre.Last().Name.ShouldBe(sourceData.Genre.Last().TagValue);

            result.Country.Count.ShouldBe(sourceData.Country.Count);
            result.Country.First().Name.ShouldBe(sourceData.Country.First().TagValue);

            result.Role.Count.ShouldBe(sourceData.Role.Count);
            result.Role.First().Name.ShouldBe(sourceData.Role.First().TagValue);
            result.Role.Last().Name.ShouldBe(sourceData.Role.Last().TagValue);

            result.Guids.Count.ShouldBe(sourceData.Guids.Count);
            result.Guids.First().Id.ShouldBe(sourceData.Guids.First().Id);
            result.Guids.Last().Id.ShouldBe(sourceData.Guids.Last().Id);

            // Media list should be empty for TV show at show level
            result.Media.Count.ShouldBe(0);
        }
    }
}
