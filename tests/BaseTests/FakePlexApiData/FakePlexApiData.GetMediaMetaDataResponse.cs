using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    public static GetMetadataItemResponse GetMediaMetaDataAsync(
        HttpStatusCode statusCode,
        Seed seed,
        LibrarySection library,
        MediaContainerWithMetadata? responseBody = null,
        HttpRequestMessage? request = null,
        Action<PlexApiDataConfig>? options = null
    )
    {
        return new Faker<GetMetadataItemResponse>()
            .StrictMode(true)
            .Ignore(x => x.Headers)
            .RuleFor(x => x.StatusCode, _ => (int)statusCode)
            .RuleFor(x => x.ContentType, _ => ContentType.ApplicationJson)
            .RuleFor(
                x => x.MediaContainerWithMetadata,
                _ => responseBody ?? GetMediaMetaDataResponseBodyResponse(seed, library, 100, options)
            )
            .RuleFor(
                x => x.RawResponse,
                (_, res) => GetHttpResponseMessage(statusCode, res.MediaContainerWithMetadata, request)
            )
            .UseSeed(seed.Next())
            .Generate();
    }

    public static MediaContainerWithMetadata GetMediaMetaDataResponseBodyResponse(
        Seed seed,
        LibrarySection library,
        int mediaCount = 0,
        Action<PlexApiDataConfig>? options = null
    )
    {
        var type = library.Type.ToPlexMediaType();

        return new MediaContainerWithMetadata
        {
            MediaContainer = _getMediaMetaDataMediaContainer
                .UseSeed(seed.Next())
                .FinishWith(
                    (_, x) =>
                    {
                        x.Metadata = GetMediaMetaDataMetadata(seed, type, options).Generate(mediaCount);
                        x.Size = x.Metadata!.Count;
                    }
                )
                .Generate(),
        };
    }

    public static Faker<Metadata> GetMediaMetaDataMetadata(
        Seed seed,
        PlexMediaType type,
        Action<PlexApiDataConfig>? options = null
    )
    {
        var config = PlexApiDataConfig.FromOptions(options);
        return _getMediaMetaDataMetadata
            .UseSeed(seed.Next())
            .RuleFor(
                l => l.Role,
                _ => _getMediaMetaDataRole.GenerateUnique(config.RolePerMediaItemCount, x => x.TagValue)
            )
            .RuleFor(
                l => l.Genre,
                _ => _getMediaMetaDataGenre.GenerateUnique(config.GenrePerMediaItemCount, x => x.TagValue)
            )
            .RuleFor(
                l => l.Country,
                _ => _getMediaMetaDataCountry.GenerateUnique(config.CountriesPerMediaItemCount, x => x.TagValue)
            )
            .FinishWith(
                (f, x) =>
                {
                    x.Type = type.ToPlexApiString();
                    x.Media = [GetMediaMetaDataMedia(seed).Generate()];
                    x.Guid = f.PlexMedia().Guid(type);
                    if (type == PlexMediaType.PhotoAlbum)
                    {
                        x.Type = "photo";
                        x.Key = $"/library/metadata/{x.RatingKey}/children";
                        x.Media = [];
                    }
                    else if (type == PlexMediaType.PhotoImage)
                    {
                        foreach (var media in x.Media)
                        {
                            media.Container = "jpg";
                            media.VideoCodec = "";
                            media.AudioCodec = "";
                            foreach (var part in media.Part!)
                            {
                                part.File = $"/fixture/photos/photo-{part.Id}.jpg";
                                part.Stream = [];
                            }
                        }
                    }
                }
            );
    }

    public static Faker<Media> GetMediaMetaDataMedia(Seed seed)
    {
        return _getMediaMetaDataMedia
            .UseSeed(seed.Next())
            .FinishWith(
                (_, x) =>
                {
                    x.Part = [GetMediaMetaDataPart(seed).Generate()];
                }
            );
    }

    public static Faker<Part> GetMediaMetaDataPart(Seed seed) =>
        _getMediaMetaDataPartFaker
            .UseSeed(seed.Next())
            .FinishWith(
                (_, x) =>
                {
                    x.Stream = [_getMediaMetaDataStreamFaker.Generate()];
                }
            );
}
