namespace Reaparr.Application.Contracts;

public class PlexMediaSlimDTOValidator : AbstractValidator<PlexMediaSlimDTO>
{
    public PlexMediaSlimDTOValidator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Year).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Duration).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MediaSize).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChildCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AddedAt).NotEmpty();
        RuleFor(x => x.UpdatedAt).NotEmpty();
        RuleFor(x => x.PlexLibraryId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlexServerId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Type)
            .Must(x =>
                x == PlexMediaType.Movie
                || x == PlexMediaType.TvShow
                || x == PlexMediaType.Season
                || x == PlexMediaType.Episode
            );

        RuleFor(x => x.UpdatedAt).NotEmpty();
        RuleForEach(x => x.Qualities)
            .ChildRules(y =>
            {
                y.RuleFor(z => z.VideoQuality).NotEqual(VideoQuality.Unknown);
            });
    }
}
