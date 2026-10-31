using Reaparr.BaseTests;

namespace Reaparr.PlexApi.UnitTests;

public class PlexMediaDataMapperStreamsUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldCreateTypedCandidatesForEveryLeafMediaDataKind_WhenPartContainsSupportedStreams()
    {
        // Arrange
        var part = CreatePart(
            CreateStream(201, "/library/streams/201", StreamType.Audio, null, "flac"),
            CreateStream(202, "/library/streams/202", StreamType.Subtitle, " SRT ", "ass"),
            CreateStream(203, "/library/streams/203", StreamType.Lyrics, null, "lrc")
        );

        // Act
        var movie = part.ToPlexMovieDataStreams();
        var episode = part.ToPlexTvShowEpisodeDataStreams();
        var track = part.ToPlexMusicTrackDataStreams();
        var clip = part.ToPlexOtherVideoDataStreams();

        // Assert
        movie
            .Select(x => (x.PlexApiStreamId, x.StreamType, x.SidecarFormat))
            .ShouldBe([
                (201, StreamType.Audio, null),
                (202, StreamType.Subtitle, "srt"),
                (203, StreamType.Lyrics, "lrc"),
            ]);
        episode.Select(x => x.PlexApiStreamId).ShouldBe([201, 202, 203]);
        track.Select(x => x.PlexApiStreamId).ShouldBe([201, 202, 203]);
        clip.Select(x => x.PlexApiStreamId).ShouldBe([201, 202, 203]);

        var firstMovieStream = movie.First();
        firstMovieStream.Language.LanguageCode.ShouldBe("ENG");
        firstMovieStream.Language.LanguageTag.ShouldBe("ENG");
    }

    [Test]
    public void ShouldAttachAcceptedStreamsToEveryMoviePart_WhenMediaContainsMultipleParts()
    {
        // Arrange
        var source = FakeData
            .GetLibraryMediaItemDTO(
                new Seed(301),
                options: config => config.IncludeMultiPartMovies = true,
                mediaType: PlexMediaType.Movie
            )
            .Generate();
        var expectedPartIds = source.Media.SelectMany(x => x.Parts).Select(x => x.Id).ToList();
        var expectedPartCount = expectedPartIds.Count;

        // Act
        var movie = source.ToPlexMovie();

        // Assert
        movie.MediaDataList.Count.ShouldBe(expectedPartCount);
        movie.MediaDataList.Select(x => x.PlexApiPartId).ShouldBe(expectedPartIds);
        movie
            .MediaDataList.Select(x => x.Streams.Select(stream => stream.StreamType).ToList())
            .ShouldAllBe(streams => streams.Contains(StreamType.Audio));
    }

    [Test]
    public void ShouldPreserveLanguageAndInitializeTransientValues_WhenMappingAnyStream()
    {
        // Arrange
        var part = CreatePart(CreateStream(201, null));

        // Act
        var streams = part.ToPlexMovieDataStreams();

        // Assert
        var stream = streams.ShouldHaveSingleItem();
        stream.Language.LanguageCode.ShouldBe("ENG");
        stream.Language.LanguageTag.ShouldBe("ENG");
        stream.IsExternal.ShouldBeNull();
        stream.MediaDataId.ShouldBe(0);
        stream.LanguageId.ShouldBe(0);
        stream.Id.ShouldBe(0);
    }

    [Test]
    public void ShouldNormalizeFormatAndUseVerifiedCodecFallbacks_WhenMappingSidecarStreams()
    {
        // Arrange
        var explicitFormat = CreateStream(201, null, StreamType.Subtitle, "  SRT  ", "ass");
        var codecFallback = CreateStream(202, null, StreamType.Subtitle, null, "SRT");
        var lyricFallback = CreateStream(203, null, StreamType.Lyrics, null, "lrc");
        var unknownCodec = CreateStream(204, null, StreamType.Subtitle, null, "ass");
        var unknownFormat = CreateStream(205, null, StreamType.Subtitle, "vtt", "srt");
        var audioFormat = CreateStream(206, null, StreamType.Audio, "srt", "flac");

        // Act
        var streams = CreatePart(explicitFormat, codecFallback, lyricFallback, unknownCodec, unknownFormat, audioFormat)
            .ToPlexMovieDataStreams();

        // Assert
        streams
            .Select(x => (x.PlexApiStreamId, x.SidecarFormat))
            .ShouldBe([(201, "srt"), (202, "srt"), (203, "lrc"), (204, null), (205, "vtt"), (206, null)]);
    }

    [Test]
    public void ShouldMapVideoAndUnknownStreamsAlongsidePersistableTypes()
    {
        // Arrange
        var part = CreatePart(
            CreateStream(201, null, StreamType.Video, null, "h264"),
            CreateStream(202, null, StreamType.Unknown, null, "unknown"),
            CreateStream(203, null, StreamType.Audio, null, "flac"),
            CreateStream(204, null, StreamType.Subtitle, null, "srt"),
            CreateStream(205, null, StreamType.Lyrics, null, "lrc")
        );

        // Act
        var streams = part.ToPlexMovieDataStreams();

        // Assert
        streams.Select(x => x.PlexApiStreamId).ShouldBe([201, 202, 203, 204, 205]);
        streams
            .Select(x => x.StreamType)
            .ShouldBe([StreamType.Video, StreamType.Unknown, StreamType.Audio, StreamType.Subtitle, StreamType.Lyrics]);
    }

    private static LibraryMediaItemPartDTO CreatePart(params LibraryMediaItemStreamDTO[] streams) =>
        new()
        {
            Id = 101,
            Key = "/library/parts/101/file.mkv",
            Duration = 1,
            File = "/media/file.mkv",
            Size = 1,
            Container = "mkv",
            Stream = streams.ToList(),
        };

    private static LibraryMediaItemStreamDTO CreateStream(
        int id,
        string? key,
        StreamType streamType = StreamType.Subtitle,
        string? format = null,
        string codec = "srt"
    ) =>
        FakeData.GetLibraryMediaItemStreamDTO(new Seed(id), streamType).Generate() with
        {
            Id = id,
            Key = key,
            Format = format,
            Codec = codec,
            LanguageCode = "ENG",
            LanguageTag = "ENG",
        };
}
