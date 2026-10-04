namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexPhotoAlbum> _plexPhotoAlbum = new Faker<PlexPhotoAlbum>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Title, f => f.Lorem.Sentence(3))
        .RuleFor(x => x.Guid, f => $"plex://photoalbum/{f.Random.Guid():N}")
        .RuleFor(x => x.Guid_IMDB, _ => null)
        .RuleFor(x => x.Guid_TMDB, _ => null)
        .RuleFor(x => x.Guid_TVDB, _ => null)
        .Ignore(x => x.Photos)
        .FinishWith(
            (_, album) =>
            {
                foreach (var photo in album.Photos)
                {
                    photo.PlexPhotoAlbum = album;
                    photo.PlexPhotoAlbumId = album.Id;
                    photo.ParentKey = album.PlexApiRatingKey;
                    photo.FullTitle = $"{album.Title}/{photo.Title}";
                }

                album.ChildCount = album.Photos.Count;
                album.MediaSize = album.Photos.Sum(x => x.MediaSize);
                album.Duration = album.Photos.Sum(x => x.Duration);
                album.Quality = album.Photos.Count == 0 ? VideoQuality.Unknown : album.Photos.Max(x => x.Quality);
                album.FullTitle = album.Title;
            }
        );

    public static Faker<PlexPhotoAlbum> GetPlexPhotoAlbums(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);
        var photoFaker = config.PhotoCount == 0 ? null : GetPlexPhotos(seed, options);
        var clipFaker = config.PhotoClipCount == 0 ? null : GetPlexPhotos(seed, options, isClip: true);

        return _plexPhotoAlbum
            .Clone()
            .RuleFor(
                x => x.Photos,
                _ =>
                {
                    var photos = photoFaker?.Generate(config.PhotoCount) ?? [];
                    if (clipFaker is not null)
                        photos.AddRange(clipFaker.Generate(config.PhotoClipCount));
                    return photos;
                }
            )
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexPhoto> _plexPhoto = new Faker<PlexPhoto>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Title, f => f.Lorem.Sentence(3))
        .RuleFor(x => x.Guid, (_, x) => $"plex://photo/{x.PlexApiRatingKey}")
        .RuleFor(x => x.Guid_IMDB, _ => null)
        .RuleFor(x => x.Guid_TMDB, _ => null)
        .RuleFor(x => x.Guid_TVDB, _ => null)
        .RuleFor(x => x.ChildCount, _ => 0)
        .RuleFor(x => x.SortIndex, f => f.IndexFaker + 1)
        .Ignore(x => x.PlexPhotoAlbumId)
        .Ignore(x => x.PlexPhotoAlbum)
        .Ignore(x => x.MediaDataList)
        .FinishWith(
            (_, photo) =>
            {
                var original = photo.MediaDataList.Single();
                original.PlexPhoto = photo;
                original.PlexPhotoId = photo.Id;
                original.UpdateInitProperty(nameof(original.PlexApiRatingKey), photo.PlexApiRatingKey);
                photo.FullTitle = photo.Title;
                photo.Duration = original.Duration / 1000;
                photo.MediaSize = original.Size;
                photo.Quality = original.Quality;
            }
        );

    public static Faker<PlexPhoto> GetPlexPhotos(Seed seed, Action<FakeDataConfig>? options = null, bool isClip = false)
    {
        var mediaDataFaker = GetPlexPhotoMediaData(seed, options, isClip);
        return _plexPhoto.Clone().RuleFor(x => x.MediaDataList, _ => mediaDataFaker.Generate(1)).UseSeed(seed.Next());
    }

    private static readonly Faker<PlexPhotoMediaData> _plexPhotoMediaData = new Faker<PlexPhotoMediaData>()
        .ApplyBasePlexMediaData(new FakeDataConfig())
        .Ignore(x => x.PlexPhotoId)
        .Ignore(x => x.PlexPhoto)
        .RuleFor(x => x.Width, _ => 4000)
        .RuleFor(x => x.Height, _ => 3000)
        .RuleFor(x => x.VideoCodec, _ => string.Empty)
        .RuleFor(x => x.AudioCodec, _ => string.Empty)
        .RuleFor(x => x.VideoResolution, _ => VideoQuality.Unknown)
        .RuleFor(x => x.Quality, (_, x) => x.VideoResolution)
        .RuleFor(x => x.Duration, _ => 0)
        .RuleFor(x => x.Container, _ => "jpg")
        .RuleFor(x => x.OriginalFilename, (_, x) => $"photo-{x.PlexApiPartId}.{x.Container}")
        .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.{x.Container}")
        .RuleFor(x => x.Source, _ => ReleaseSource.None)
        .RuleFor(x => x.GeneratedNameSyncedAt, _ => null);

    private static readonly Faker<PlexPhotoMediaData> _plexPhotoClipMediaData = _plexPhotoMediaData
        .Clone()
        .RuleFor(x => x.Width, _ => 1920)
        .RuleFor(x => x.Height, _ => 1080)
        .RuleFor(x => x.VideoCodec, _ => "h264")
        .RuleFor(x => x.AudioCodec, _ => "aac")
        .RuleFor(x => x.VideoResolution, _ => VideoQuality.FullHD)
        .RuleFor(x => x.Duration, f => f.Random.Int(1000, 120000))
        .RuleFor(x => x.Container, _ => "mp4");

    public static Faker<PlexPhotoMediaData> GetPlexPhotoMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null,
        bool isClip = false
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var faker = (isClip ? _plexPhotoClipMediaData : _plexPhotoMediaData).Clone();
        if (config.DownloadFileSizeInMb > 0)
            faker.RuleFor(x => x.Size, (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes);
        return faker.UseSeed(seed.Next());
    }
}
