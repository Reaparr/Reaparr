using LukeHagar.PlexAPI.SDK.Models.Components;
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
            var now = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
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
                OriginallyAvailableAt = "2023-01-01",
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
            result.AddedAt.ShouldBe(now.UtcDateTime);
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
        public void ShouldMapMissingMetadataDefaults_WhenApiResponseOmitsOptionalFields()
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
            result.AddedAt.ShouldBe(DateTime.UnixEpoch);
            result.OriginallyAvailableAt.ShouldBe(string.Empty);
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
            var now = new DateTimeOffset(2020, 5, 15, 0, 0, 0, TimeSpan.Zero);
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
                OriginallyAvailableAt = "2020-05-15",
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
            result.AddedAt.ShouldBe(now.UtcDateTime);
            result.UpdatedAt.ShouldBe(DateTimeExtensions.FromUnixTime(sourceData.UpdatedAt ?? 0));

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

        [Test]
        public void ShouldInferFullHdQuality_WhenMetadataIsNonPhotoClip()
        {
            // Arrange
            var sourceData = new Metadata
            {
                RatingKey = "789",
                Type = "clip",
                Subtype = "video",
                Title = "Other video",
                Media = [new Media { Id = 1, VideoResolution = "1080" }],
            };

            // Act
            var result = sourceData.ToMediaItemDTO();

            result.Type.ShouldBe(PlexMediaType.OtherVideos);
            result.Media.Single().VideoResolution.ShouldBe(VideoQuality.FullHD);
        }

        [Test]
        public void ShouldKeepQualityUnknown_WhenMetadataIsPhotoClip()
        {
            // Arrange
            var sourceData = new Metadata
            {
                RatingKey = "790",
                Type = "clip",
                Subtype = "photo",
                Title = "Photo clip",
                Media = [new Media { Id = 2, VideoResolution = "1080" }],
            };

            // Act
            var result = sourceData.ToMediaItemDTO();

            // Assert
            result.Media.Single().VideoResolution.ShouldBe(VideoQuality.Unknown);
        }

        [Test]
        public void ShouldPreserveEnrichedAudioAndSubtitleStreams_WhenTrackResponseContainsOptionalStreams()
        {
            // Arrange
            var source = new Metadata
            {
                RatingKey = "78658",
                Key = "/library/metadata/78658",
                Type = "track",
                Title = "Strangers By Nature",
                Media =
                [
                    new Media
                    {
                        Id = 9001,
                        AudioCodec = "flac",
                        Bitrate = 737,
                        AudioChannels = 2,
                        Part =
                        [
                            new Part
                            {
                                Id = 9101,
                                Key = "/library/parts/9101/file.flac",
                                File = "/music/30/01.flac",
                                Container = "flac",
                                Stream =
                                [
                                    new Stream
                                    {
                                        Id = 1,
                                        StreamType = LukeHagar.PlexAPI.SDK.Models.Components.StreamType.Audio,
                                        Selected = true,
                                        Default = true,
                                        Codec = "flac",
                                        Bitrate = 737,
                                        BitDepth = 16,
                                        SamplingRate = 44100,
                                        Channels = 2,
                                        AudioChannelLayout = "stereo",
                                    },
                                    new Stream
                                    {
                                        Id = 2,
                                        StreamType = LukeHagar.PlexAPI.SDK.Models.Components.StreamType.Subtitle,
                                        Codec = "lrc",
                                        Title = "Lyrics",
                                    },
                                ],
                            },
                        ],
                    },
                ],
            };

            // Act
            var result = source.ToMediaItemDTO();

            // Assert
            var streams = result.Media.ShouldHaveSingleItem().Parts.ShouldHaveSingleItem().Stream;
            streams.Select(x => x.StreamType).ShouldBe([Domain.StreamType.Audio, Domain.StreamType.Subtitle]);
            var audio = streams[0];
            (
                audio.Codec,
                audio.Bitrate,
                audio.BitDepth,
                audio.SamplingRate,
                audio.Channels,
                audio.AudioChannelLayout,
                audio.Selected,
                audio.Default
            ).ShouldBe(("flac", 737, 16, 44100, 2, "stereo", true, true));
        }

        [Test]
        public void ShouldMapNumericFourToLyrics_WhilePreservingRawLocatorAndFormat()
        {
            // Arrange
            var source = new Metadata
            {
                RatingKey = "78659",
                Key = "/library/metadata/78659",
                Type = "track",
                Title = "Lyrics track",
                Media =
                [
                    new Media
                    {
                        Id = 9002,
                        Part =
                        [
                            new Part
                            {
                                Id = 9102,
                                Key = "/library/parts/9102/file.flac",
                                Stream =
                                [
                                    new Stream
                                    {
                                        Id = 4,
                                        Key = null!,
                                        Format = "  LRC ",
                                        StreamType = (LukeHagar.PlexAPI.SDK.Models.Components.StreamType)4,
                                        Codec = "lrc",
                                        LanguageCode = null,
                                        LanguageTag = "ENG",
                                    },
                                ],
                            },
                        ],
                    },
                ],
            };

            // Act
            var result = source.ToMediaItemDTO();

            // Assert
            var stream = result.Media.ShouldHaveSingleItem().Parts.ShouldHaveSingleItem().Stream.ShouldHaveSingleItem();
            stream.StreamType.ShouldBe(Domain.StreamType.Lyrics);
            stream.Key.ShouldBeNull();
            stream.Format.ShouldBe("  LRC ");
            stream.LanguageCode.ShouldBe(string.Empty);
            stream.LanguageTag.ShouldBe("ENG");
        }

        [Test]
        public void ShouldRetainStreamsForContainersAndPhotoClips_WhileKeepingMediaMetadata()
        {
            // Arrange
            var container = new Metadata
            {
                RatingKey = "78660",
                Key = "/library/metadata/78660",
                Type = "show",
                Title = "Container",
                Media = [CreateMediaWithStream(9003, 9103)],
            };
            var photoClip = new Metadata
            {
                RatingKey = "78661",
                Key = "/library/metadata/78661",
                Type = "clip",
                Subtype = "photo",
                Title = "Photo clip",
                Media = [CreateMediaWithStream(9004, 9104)],
            };

            // Act
            var containerResult = container.ToMediaItemDTO();
            var photoResult = photoClip.ToMediaItemDTO();

            // Assert
            var containerStream = containerResult
                .Media.ShouldHaveSingleItem()
                .Parts.ShouldHaveSingleItem()
                .Stream.ShouldHaveSingleItem();
            var photoStream = photoResult
                .Media.ShouldHaveSingleItem()
                .Parts.ShouldHaveSingleItem()
                .Stream.ShouldHaveSingleItem();
            containerStream.StreamType.ShouldBe(Domain.StreamType.Subtitle);
            photoStream.StreamType.ShouldBe(Domain.StreamType.Subtitle);
            containerResult.Media.ShouldHaveSingleItem().Id.ShouldBe(9003);
            photoResult.Media.ShouldHaveSingleItem().Id.ShouldBe(9004);
        }

        private static Media CreateMediaWithStream(int mediaId, int partId) =>
            new()
            {
                Id = mediaId,
                Part =
                [
                    new Part
                    {
                        Id = partId,
                        Key = $"/library/parts/{partId}",
                        Stream =
                        [
                            new Stream
                            {
                                Id = 1,
                                Key = "/library/streams/1",
                                StreamType = LukeHagar.PlexAPI.SDK.Models.Components.StreamType.Subtitle,
                                Codec = "srt",
                            },
                        ],
                    },
                ],
            };
    }
}
