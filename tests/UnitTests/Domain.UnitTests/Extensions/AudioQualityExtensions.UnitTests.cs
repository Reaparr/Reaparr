namespace Reaparr.Domain.UnitTests;

public class AudioQualityExtensionsUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldExposeStableAudioQualityMembers()
    {
        // Arrange
        var expectedNames = new[]
        {
            "None",
            "Unknown",
            "MP3_Below96Kbps",
            "MP3_96Kbps",
            "MP3_128Kbps",
            "MP3_160Kbps",
            "MP3_192Kbps",
            "MP3_256Kbps",
            "MP3_320KbpsPlus",
            "AAC_Below96Kbps",
            "AAC_96Kbps",
            "AAC_128Kbps",
            "AAC_160Kbps",
            "AAC_192Kbps",
            "AAC_256Kbps",
            "AAC_320KbpsPlus",
            "Vorbis_Below96Kbps",
            "Vorbis_96Kbps",
            "Vorbis_128Kbps",
            "Vorbis_160Kbps",
            "Vorbis_192Kbps",
            "Vorbis_256Kbps",
            "Vorbis_320KbpsPlus",
            "Opus_Below96Kbps",
            "Opus_96Kbps",
            "Opus_128Kbps",
            "Opus_160Kbps",
            "Opus_192Kbps",
            "Opus_256Kbps",
            "Opus_320KbpsPlus",
            "FLAC_16Bit_44_1kHz",
            "FLAC_16Bit_48kHz",
            "FLAC_16Bit_88_2kHz",
            "FLAC_16Bit_96kHz",
            "FLAC_16Bit_176_4kHz",
            "FLAC_16Bit_192kHz",
            "FLAC_24Bit_44_1kHz",
            "FLAC_24Bit_48kHz",
            "FLAC_24Bit_88_2kHz",
            "FLAC_24Bit_96kHz",
            "FLAC_24Bit_176_4kHz",
            "FLAC_24Bit_192kHz",
            "ALAC_16Bit_44_1kHz",
            "ALAC_16Bit_48kHz",
            "ALAC_16Bit_88_2kHz",
            "ALAC_16Bit_96kHz",
            "ALAC_16Bit_176_4kHz",
            "ALAC_16Bit_192kHz",
            "ALAC_24Bit_44_1kHz",
            "ALAC_24Bit_48kHz",
            "ALAC_24Bit_88_2kHz",
            "ALAC_24Bit_96kHz",
            "ALAC_24Bit_176_4kHz",
            "ALAC_24Bit_192kHz",
            "PCM_16Bit_44_1kHz",
            "PCM_16Bit_48kHz",
            "PCM_16Bit_88_2kHz",
            "PCM_16Bit_96kHz",
            "PCM_16Bit_176_4kHz",
            "PCM_16Bit_192kHz",
            "PCM_24Bit_44_1kHz",
            "PCM_24Bit_48kHz",
            "PCM_24Bit_88_2kHz",
            "PCM_24Bit_96kHz",
            "PCM_24Bit_176_4kHz",
            "PCM_24Bit_192kHz",
        };

        // Act
        var members = Enum.GetValues<AudioQuality>().OrderBy(value => (int)value).ToArray();
        var names = members.Select(value => value.ToString()).ToArray();
        var values = members.Select(value => (int)value).ToArray();

        // Assert
        names.Length.ShouldBe(66);
        names.ShouldBe(expectedNames);
        values.ShouldBe(Enumerable.Range(-1, 66).ToArray());
    }

    [Test]
    [Arguments("mp3", 1, AudioQuality.MP3_Below96Kbps)]
    [Arguments("mp3", 95, AudioQuality.MP3_Below96Kbps)]
    [Arguments("mp3", 96, AudioQuality.MP3_96Kbps)]
    [Arguments("mp3", 127, AudioQuality.MP3_96Kbps)]
    [Arguments("mp3", 128, AudioQuality.MP3_128Kbps)]
    [Arguments("mp3", 159, AudioQuality.MP3_128Kbps)]
    [Arguments("mp3", 160, AudioQuality.MP3_160Kbps)]
    [Arguments("mp3", 191, AudioQuality.MP3_160Kbps)]
    [Arguments("mp3", 192, AudioQuality.MP3_192Kbps)]
    [Arguments("mp3", 255, AudioQuality.MP3_192Kbps)]
    [Arguments("mp3", 256, AudioQuality.MP3_256Kbps)]
    [Arguments("mp3", 319, AudioQuality.MP3_256Kbps)]
    [Arguments("mp3", 320, AudioQuality.MP3_320KbpsPlus)]
    [Arguments("aac", 95, AudioQuality.AAC_Below96Kbps)]
    [Arguments("aac", 96, AudioQuality.AAC_96Kbps)]
    [Arguments("aac", 128, AudioQuality.AAC_128Kbps)]
    [Arguments("aac", 160, AudioQuality.AAC_160Kbps)]
    [Arguments("aac", 192, AudioQuality.AAC_192Kbps)]
    [Arguments("aac", 256, AudioQuality.AAC_256Kbps)]
    [Arguments("aac", 320, AudioQuality.AAC_320KbpsPlus)]
    [Arguments("vorbis", 95, AudioQuality.Vorbis_Below96Kbps)]
    [Arguments("vorbis", 96, AudioQuality.Vorbis_96Kbps)]
    [Arguments("vorbis", 128, AudioQuality.Vorbis_128Kbps)]
    [Arguments("vorbis", 160, AudioQuality.Vorbis_160Kbps)]
    [Arguments("vorbis", 192, AudioQuality.Vorbis_192Kbps)]
    [Arguments("vorbis", 256, AudioQuality.Vorbis_256Kbps)]
    [Arguments("vorbis", 320, AudioQuality.Vorbis_320KbpsPlus)]
    [Arguments("opus", 95, AudioQuality.Opus_Below96Kbps)]
    [Arguments("opus", 96, AudioQuality.Opus_96Kbps)]
    [Arguments("opus", 128, AudioQuality.Opus_128Kbps)]
    [Arguments("opus", 160, AudioQuality.Opus_160Kbps)]
    [Arguments("opus", 192, AudioQuality.Opus_192Kbps)]
    [Arguments("opus", 256, AudioQuality.Opus_256Kbps)]
    [Arguments("opus", 320, AudioQuality.Opus_320KbpsPlus)]
    public void ShouldClassifyEveryLossyBitrateBand(string codec, int bitrateKbps, AudioQuality expectedQuality)
    {
        // Arrange
        var file = CreateFile(codec, bitrateKbps: bitrateKbps);

        // Act
        var quality = file.ClassifyAudioQuality();

        // Assert
        quality.ShouldBe(expectedQuality);
    }

    [Test]
    public void ShouldClassifyEverySupportedLosslessDepthAndSampleRatePair()
    {
        // Arrange
        var pairs = new[]
        {
            (
                16,
                44100,
                AudioQuality.FLAC_16Bit_44_1kHz,
                AudioQuality.ALAC_16Bit_44_1kHz,
                AudioQuality.PCM_16Bit_44_1kHz
            ),
            (16, 48000, AudioQuality.FLAC_16Bit_48kHz, AudioQuality.ALAC_16Bit_48kHz, AudioQuality.PCM_16Bit_48kHz),
            (
                16,
                88200,
                AudioQuality.FLAC_16Bit_88_2kHz,
                AudioQuality.ALAC_16Bit_88_2kHz,
                AudioQuality.PCM_16Bit_88_2kHz
            ),
            (16, 96000, AudioQuality.FLAC_16Bit_96kHz, AudioQuality.ALAC_16Bit_96kHz, AudioQuality.PCM_16Bit_96kHz),
            (
                16,
                176400,
                AudioQuality.FLAC_16Bit_176_4kHz,
                AudioQuality.ALAC_16Bit_176_4kHz,
                AudioQuality.PCM_16Bit_176_4kHz
            ),
            (16, 192000, AudioQuality.FLAC_16Bit_192kHz, AudioQuality.ALAC_16Bit_192kHz, AudioQuality.PCM_16Bit_192kHz),
            (
                24,
                44100,
                AudioQuality.FLAC_24Bit_44_1kHz,
                AudioQuality.ALAC_24Bit_44_1kHz,
                AudioQuality.PCM_24Bit_44_1kHz
            ),
            (24, 48000, AudioQuality.FLAC_24Bit_48kHz, AudioQuality.ALAC_24Bit_48kHz, AudioQuality.PCM_24Bit_48kHz),
            (
                24,
                88200,
                AudioQuality.FLAC_24Bit_88_2kHz,
                AudioQuality.ALAC_24Bit_88_2kHz,
                AudioQuality.PCM_24Bit_88_2kHz
            ),
            (24, 96000, AudioQuality.FLAC_24Bit_96kHz, AudioQuality.ALAC_24Bit_96kHz, AudioQuality.PCM_24Bit_96kHz),
            (
                24,
                176400,
                AudioQuality.FLAC_24Bit_176_4kHz,
                AudioQuality.ALAC_24Bit_176_4kHz,
                AudioQuality.PCM_24Bit_176_4kHz
            ),
            (24, 192000, AudioQuality.FLAC_24Bit_192kHz, AudioQuality.ALAC_24Bit_192kHz, AudioQuality.PCM_24Bit_192kHz),
        };

        // Act / Assert
        foreach (var pair in pairs)
        {
            CreateFile("flac", bitDepth: pair.Item1, sampleRateHz: pair.Item2)
                .ClassifyAudioQuality()
                .ShouldBe(pair.Item3);
            CreateFile("alac", bitDepth: pair.Item1, sampleRateHz: pair.Item2)
                .ClassifyAudioQuality()
                .ShouldBe(pair.Item4);
            CreateFile("pcm", bitDepth: pair.Item1, sampleRateHz: pair.Item2)
                .ClassifyAudioQuality()
                .ShouldBe(pair.Item5);
        }
    }

    [Test]
    public void ShouldReturnUnknownForMissingUnsupportedOrUncommonAudioMeasurements()
    {
        // Arrange
        var cases = new[]
        {
            CreateFile("mp3", bitrateKbps: null),
            CreateFile("mp3", bitrateKbps: 0),
            CreateFile("mp3", bitrateKbps: -1),
            CreateFile("flac", bitDepth: null, sampleRateHz: 44100),
            CreateFile("flac", bitDepth: 20, sampleRateHz: 44100),
            CreateFile("flac", bitDepth: 32, sampleRateHz: 96000),
            CreateFile("flac", bitDepth: 16, sampleRateHz: 44101),
            CreateFile("aac", bitrateKbps: null),
            CreateFile("unsupported", bitrateKbps: 320, bitDepth: 24, sampleRateHz: 192000),
        };

        // Act / Assert
        foreach (var file in cases)
            file.ClassifyAudioQuality().ShouldBe(AudioQuality.Unknown);
    }

    [Test]
    [Arguments("mp3", AudioQuality.MP3_128Kbps)]
    [Arguments("aac", AudioQuality.AAC_128Kbps)]
    public void ShouldClassifyLossyBitrateWithoutAssumingMissingComparisonProperties(
        string codec,
        AudioQuality expected
    )
    {
        // Arrange
        var incomplete = CreateFile(codec, bitrateKbps: 128, sampleRateHz: null, channels: null, layout: null);
        var lowerBitrate = CreateFile(codec, bitrateKbps: 96, profile: "LC");

        // Act / Assert
        incomplete.ClassifyAudioQuality().ShouldBe(expected);
        incomplete.AudioBitDepth.ShouldBeNull();
        incomplete.IsHigherAudioQualityThan(lowerBitrate).ShouldBeFalse();
    }

    [Test]
    public void ShouldTreatLosslessResolutionAsEquivalentRegardlessOfCompressionBitrate()
    {
        // Arrange
        var flac = CreateFile("flac", bitrateKbps: 737, bitDepth: 16, sampleRateHz: 44100, id: 1);
        var alac = CreateFile("alac", bitrateKbps: 1200, bitDepth: 16, sampleRateHz: 44100, id: 2);

        // Act
        var equivalent = flac.IsEquivalentAudioQualityTo(alac);
        var flacHigher = flac.IsHigherAudioQualityThan(alac);
        var alacHigher = alac.IsHigherAudioQualityThan(flac);

        // Assert
        equivalent.ShouldBeTrue();
        flacHigher.ShouldBeFalse();
        alacHigher.ShouldBeFalse();
    }

    [Test]
    public void ShouldCompareSameLossyCodecUsingActualBitrateAndCompatibleProperties()
    {
        // Arrange
        var lower = CreateFile("mp3", bitrateKbps: 128, id: 1);
        var higherWithinSameBand = CreateFile("mp3", bitrateKbps: 129, id: 2);

        // Act
        var higher = higherWithinSameBand.IsHigherAudioQualityThan(lower);
        var equivalent = higherWithinSameBand.IsEquivalentAudioQualityTo(lower);
        var reverse = lower.IsHigherAudioQualityThan(higherWithinSameBand);

        // Assert
        higher.ShouldBeTrue();
        equivalent.ShouldBeFalse();
        reverse.ShouldBeFalse();
    }

    [Test]
    public void ShouldRejectLossyComparisonWhenTechnicalPropertiesAreUnknownOrIncompatible()
    {
        // Arrange
        var complete = CreateFile("mp3", bitrateKbps: 320);
        var missingSampleRate = CreateFile("mp3", bitrateKbps: 128, sampleRateHz: null);
        var differentChannels = CreateFile("mp3", bitrateKbps: 128, channels: 6, layout: "5.1");
        var differentLayout = CreateFile("mp3", bitrateKbps: 128, layout: "5.1");
        var differentCodec = CreateFile("vorbis", bitrateKbps: 128);

        // Act / Assert
        complete.IsHigherAudioQualityThan(missingSampleRate).ShouldBeFalse();
        complete.IsHigherAudioQualityThan(differentChannels).ShouldBeFalse();
        complete.IsHigherAudioQualityThan(differentLayout).ShouldBeFalse();
        complete.IsHigherAudioQualityThan(differentCodec).ShouldBeFalse();
        complete.IsEquivalentAudioQualityTo(missingSampleRate).ShouldBeFalse();
        complete.IsEquivalentAudioQualityTo(differentChannels).ShouldBeFalse();
        complete.IsEquivalentAudioQualityTo(differentLayout).ShouldBeFalse();
        complete.IsEquivalentAudioQualityTo(differentCodec).ShouldBeFalse();
    }

    [Test]
    public void ShouldRequireMatchingKnownAacProfiles()
    {
        // Arrange
        var complete = CreateFile("aac", bitrateKbps: 128, profile: "LC");
        var sameProfileHigher = CreateFile("aac", bitrateKbps: 129, profile: "lc");
        var missingProfile = CreateFile("aac", bitrateKbps: 128, profile: null);
        var differentProfile = CreateFile("aac", bitrateKbps: 128, profile: "HE");

        // Act / Assert
        sameProfileHigher.IsHigherAudioQualityThan(complete).ShouldBeTrue();
        complete.IsEquivalentAudioQualityTo(CreateFile("aac", bitrateKbps: 128, profile: "lc")).ShouldBeTrue();
        complete.IsHigherAudioQualityThan(missingProfile).ShouldBeFalse();
        complete.IsEquivalentAudioQualityTo(missingProfile).ShouldBeFalse();
        complete.IsHigherAudioQualityThan(differentProfile).ShouldBeFalse();
        complete.IsEquivalentAudioQualityTo(differentProfile).ShouldBeFalse();
    }

    [Test]
    [Arguments(16, 44100, 16, 48000)]
    [Arguments(16, 44100, 24, 96000)]
    [Arguments(16, 44100, 24, 44100)]
    public void ShouldRecognizeLosslessResolutionUpgradeWithoutRegressions(
        int ownedDepth,
        int ownedRate,
        int remoteDepth,
        int remoteRate
    )
    {
        // Arrange
        var owned = CreateFile("flac", bitDepth: ownedDepth, sampleRateHz: ownedRate);
        var remote = CreateFile("alac", bitDepth: remoteDepth, sampleRateHz: remoteRate);

        // Act / Assert
        remote.IsHigherAudioQualityThan(owned).ShouldBeTrue();
        owned.IsHigherAudioQualityThan(remote).ShouldBeFalse();
        remote.IsEquivalentAudioQualityTo(owned).ShouldBeFalse();
        new[] { owned, remote }.GetBestAudioFile().ShouldBe(remote);
    }

    [Test]
    public void ShouldRejectSuppliedIncompatibleLosslessProfiles()
    {
        // Arrange
        var owned = CreateFile("flac", bitDepth: 16, profile: "profile-a");
        var remote = CreateFile("flac", bitDepth: 24, profile: "profile-b");

        // Act / Assert
        remote.IsHigherAudioQualityThan(owned).ShouldBeFalse();
        remote.IsEquivalentAudioQualityTo(owned).ShouldBeFalse();
        new[] { owned, remote }.GetBestAudioFile().ShouldBeNull();
    }

    [Test]
    public void ShouldTreatMixedLosslessRegressionsAndDifferentLossyCodecsAsIncomparable()
    {
        // Arrange
        var higherBitDepth = CreateFile("flac", bitDepth: 24, sampleRateHz: 96000);
        var higherSampleRate = CreateFile("flac", bitDepth: 16, sampleRateHz: 192000);
        var mp3 = CreateFile("mp3", bitrateKbps: 320);
        var vorbis = CreateFile("vorbis", bitrateKbps: 320);

        // Act / Assert
        higherBitDepth.IsHigherAudioQualityThan(higherSampleRate).ShouldBeFalse();
        higherSampleRate.IsHigherAudioQualityThan(higherBitDepth).ShouldBeFalse();
        higherBitDepth.IsEquivalentAudioQualityTo(higherSampleRate).ShouldBeFalse();
        mp3.IsHigherAudioQualityThan(vorbis).ShouldBeFalse();
        vorbis.IsHigherAudioQualityThan(mp3).ShouldBeFalse();
    }

    [Test]
    public void ShouldAllowRecognizedLosslessUpgradeOnlyWithCompatibleLossyProperties()
    {
        // Arrange
        var lossless = CreateFile("flac", bitDepth: 16, sampleRateHz: 44100);
        var lossy = CreateFile("mp3", bitrateKbps: 320);
        var differentRate = CreateFile("mp3", bitrateKbps: 320, sampleRateHz: 48000);
        var missingLayout = CreateFile("mp3", bitrateKbps: 320, layout: null);
        var aacWithoutProfile = CreateFile("aac", bitrateKbps: 320, profile: null);
        var aacWithProfile = CreateFile("aac", bitrateKbps: 320, profile: "LC");

        // Act / Assert
        lossless.IsHigherAudioQualityThan(lossy).ShouldBeTrue();
        lossless.IsHigherAudioQualityThan(differentRate).ShouldBeFalse();
        lossless.IsHigherAudioQualityThan(missingLayout).ShouldBeFalse();
        lossless.IsHigherAudioQualityThan(aacWithoutProfile).ShouldBeFalse();
        lossless.IsHigherAudioQualityThan(aacWithProfile).ShouldBeTrue();
        lossy.IsHigherAudioQualityThan(lossless).ShouldBeFalse();
    }

    [Test]
    public void ShouldReturnNoBestAudioFileWhenCandidatesAreIncomparable()
    {
        // Arrange
        var files = new[]
        {
            CreateFile("flac", bitDepth: 24, sampleRateHz: 96000, id: 1),
            CreateFile("flac", bitDepth: 16, sampleRateHz: 192000, id: 2),
        };

        // Act
        var best = files.GetBestAudioFile();

        // Assert
        best.ShouldBeNull();
    }

    [Test]
    public void ShouldSelectDeterministicEquivalentBestAudioFile()
    {
        // Arrange
        var later = CreateFile(
            "flac",
            bitrateKbps: 900,
            bitDepth: 16,
            sampleRateHz: 44100,
            source: ReleaseSource.WebDl,
            mediaId: 20,
            partId: 2,
            id: 2
        );
        var earlier = CreateFile(
            "alac",
            bitrateKbps: 1200,
            bitDepth: 16,
            sampleRateHz: 44100,
            source: ReleaseSource.BluRay,
            mediaId: 10,
            partId: 1,
            id: 1
        );

        // Act
        var best = new[] { later, earlier }.GetBestAudioFile();

        // Assert
        best.ShouldBe(earlier);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldHonorExplicitAudioSelectionBeforeAutomaticQuality(bool reverse)
    {
        var selected = CreateFile("mp3", bitrateKbps: 128, id: 10);
        var higher = CreateFile("mp3", bitrateKbps: 320, id: 20);
        selected.AudioQuality = AudioQuality.MP3_128Kbps;
        higher.AudioQuality = AudioQuality.MP3_320KbpsPlus;
        var files = reverse ? new[] { higher, selected } : new[] { selected, higher };

        var result = files.PickAudioQuality(selected.Id);

        result.File.ShouldBeSameAs(selected);
        result.Quality.ShouldBe(AudioQuality.MP3_128Kbps);
    }

    [Test]
    [Arguments(null)]
    [Arguments(999)]
    public void ShouldSelectBestComparableAudioWhenExplicitSelectionIsAbsentOrStale(int? selectedId)
    {
        var lower = CreateFile("mp3", bitrateKbps: 128, id: 10);
        var higher = CreateFile("mp3", bitrateKbps: 320, id: 20);
        lower.AudioQuality = AudioQuality.MP3_128Kbps;
        higher.AudioQuality = AudioQuality.MP3_320KbpsPlus;

        var result = new[] { lower, higher }.PickAudioQuality(selectedId);

        result.File.ShouldBeSameAs(higher);
        result.Quality.ShouldBe(AudioQuality.MP3_320KbpsPlus);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldPreserveUnknownAndDeterministicFileIdentityWhenAudioIsIncomparable(bool reverse)
    {
        var earlier = CreateFile("flac", bitDepth: 16, sampleRateHz: 192000, mediaId: 10, id: 20);
        var later = CreateFile("flac", bitDepth: 24, sampleRateHz: 96000, mediaId: 20, id: 10);
        earlier.AudioQuality = AudioQuality.FLAC_16Bit_192kHz;
        later.AudioQuality = AudioQuality.FLAC_24Bit_96kHz;
        var files = reverse ? new[] { later, earlier } : new[] { earlier, later };

        var result = files.PickAudioQuality();

        result.File.ShouldBeSameAs(earlier);
        result.Quality.ShouldBe(AudioQuality.Unknown);
    }

    [Test]
    public void ShouldAllowExplicitAudioSelectionWhenAutomaticCandidatesAreIncomparable()
    {
        var selected = CreateFile("flac", bitDepth: 24, sampleRateHz: 96000, id: 10);
        var other = CreateFile("flac", bitDepth: 16, sampleRateHz: 192000, id: 20);
        selected.AudioQuality = AudioQuality.FLAC_24Bit_96kHz;

        var result = new[] { other, selected }.PickAudioQuality(selected.Id);

        result.File.ShouldBeSameAs(selected);
        result.Quality.ShouldBe(AudioQuality.FLAC_24Bit_96kHz);
    }

    [Test]
    public void ShouldHaveNoAudioRepresentativeForAnEmptyInventory()
    {
        var result = Array.Empty<PlexMusicTrackMediaData>().PickAudioQuality();

        result.File.ShouldBeNull();
        result.Quality.ShouldBe(AudioQuality.Unknown);
    }

    private static PlexMusicTrackMediaData CreateFile(
        string codec,
        int? bitrateKbps = 128,
        int? bitDepth = null,
        int? sampleRateHz = 44100,
        int? channels = 2,
        string? layout = "stereo",
        string? profile = null,
        ReleaseSource source = ReleaseSource.None,
        int mediaId = 1,
        int partId = 1,
        int id = 1
    ) =>
        new()
        {
            Id = id,
            PlexApiRatingKey = 1,
            PlexApiMediaId = mediaId,
            PlexApiPartId = partId,
            PlexTrackId = 1,
            PartIndex = 0,
            VideoQuality = VideoQuality.None,
            Container = "test",
            Duration = 1,
            Size = 1,
            Key = "/library/parts/1/file",
            Source = source,
            VideoCodec = string.Empty,
            AudioCodec = codec,
            OriginalFilename = "track.test",
            PlexLibraryId = 1,
            PlexServerId = 1,
            AudioBitrateKbps = bitrateKbps,
            AudioBitDepth = bitDepth,
            AudioSampleRateHz = sampleRateHz,
            AudioChannels = channels,
            AudioChannelLayout = layout,
            AudioProfile = profile,
            AudioQuality = AudioQuality.Unknown,
        };
}
