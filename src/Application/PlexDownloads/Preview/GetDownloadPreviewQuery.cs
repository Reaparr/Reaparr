namespace Reaparr.Application;

public record GetDownloadPreviewQuery(DownloadPreviewRequest Request) : ICommand<Result<List<DownloadPreview>>>;

public class GetDownloadPreviewQueryValidator : AbstractValidator<GetDownloadPreviewQuery>
{
    public GetDownloadPreviewQueryValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.DownloadMedias).NotNull().NotEmpty();
                RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
                RuleForEach(x => x.Request.DownloadMedias)
                    .Must(x => x is null || !x.Type.IsPhotoType() || x.Qualities is { Count: 0 })
                    .WithMessage("Photo selections must not include quality or part selectors.");
            });
    }
}

public class GetDownloadPreviewQueryHandler : ICommandHandler<GetDownloadPreviewQuery, Result<List<DownloadPreview>>>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetDownloadPreviewQueryHandler(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    /// <summary>
    /// Handles the GetDownloadPreviewQuery by generating download previews for supported media selections.
    /// </summary>
    public async Task<Result<List<DownloadPreview>>> ExecuteAsync(
        GetDownloadPreviewQuery request,
        CancellationToken cancellationToken
    )
    {
        var downloadPreviews = new List<DownloadPreview>();

        if (!request.Request.DownloadMedias.Any())
            return Result.Ok(downloadPreviews);

        var movieSelections = request.Request.DownloadMedias.Where(x => x.Type.IsMovieType()).ToList();
        var movieResult = await _commandExecutor.Send(
            new GetMovieDownloadPreviewCommand(movieSelections),
            cancellationToken
        );
        if (movieResult.IsCancelledOrFailed)
            return movieResult.ToResult();
        downloadPreviews.AddRange(movieResult.Value);

        var tvSelections = request.Request.DownloadMedias.Where(x => x.Type.IsTvType()).ToList();
        var tvResult = await _commandExecutor.Send(
            new GetTvShowDownloadPreviewCommand(tvSelections),
            cancellationToken
        );
        if (tvResult.IsCancelledOrFailed)
            return tvResult.ToResult();
        downloadPreviews.AddRange(tvResult.Value);

        var musicSelections = request.Request.DownloadMedias.Where(x => x.Type.IsMusicType()).ToList();
        var musicResult = await _commandExecutor.Send(
            new GetMusicDownloadPreviewCommand(musicSelections),
            cancellationToken
        );
        if (musicResult.IsCancelledOrFailed)
            return musicResult.ToResult().LogIfFailed();
        downloadPreviews.AddRange(musicResult.Value);

        var otherVideoSelections = request.Request.DownloadMedias.Where(x => x.Type.IsOtherVideosType()).ToList();
        var otherVideoResult = await _commandExecutor.Send(
            new GetOtherVideoDownloadPreviewCommand(otherVideoSelections),
            cancellationToken
        );
        if (otherVideoResult.IsCancelledOrFailed)
            return otherVideoResult.ToResult().LogIfFailed();
        downloadPreviews.AddRange(otherVideoResult.Value);

        var photoSelections = request.Request.DownloadMedias.Where(x => x.Type.IsPhotoType()).ToList();
        var photoRequest = new DownloadPreviewRequest
        {
            DownloadMedias = photoSelections,
            DestinationFolderPathId = request.Request.DestinationFolderPathId,
            CustomDestinationFolderPath = request.Request.CustomDestinationFolderPath,
        };
        var photoResult = await _commandExecutor.Send(
            new GetPhotoDownloadPreviewCommand(photoRequest),
            cancellationToken
        );
        if (photoResult.IsCancelledOrFailed)
            return photoResult.ToResult().LogIfFailed();
        downloadPreviews.AddRange(photoResult.Value);

        return Result.Ok(downloadPreviews);
    }
}
