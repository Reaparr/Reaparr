namespace Reaparr.Domain;

public class DownloadTaskEpisodeValidator : AbstractValidator<DownloadTaskTvShowEpisode>
{
    public DownloadTaskEpisodeValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.PlexApiRatingKey).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.FullTitle).NotEmpty();
        RuleFor(x => x.Year).GreaterThan(0);
        RuleFor(x => x.DataTotal).GreaterThan(0);
        RuleFor(x => x.DownloadStatus).Equal(DownloadStatus.Queued);
        RuleFor(x => x.CreatedAt).GreaterThan(DateTime.MinValue);

        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.PlexServerId).GreaterThan(0);

        RuleFor(x => x.DownloadTaskType).Equal(DownloadTaskType.Episode);
        RuleFor(x => x.MediaType).Equal(PlexMediaType.Episode);
        RuleFor(x => x.DataTotal).GreaterThan(0);
        RuleForEach(x => x.Children).SetValidator(new DownloadTaskEpisodeFileValidator());
    }
}
