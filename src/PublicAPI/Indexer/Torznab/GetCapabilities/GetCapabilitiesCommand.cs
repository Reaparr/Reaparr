namespace Reaparr.PublicAPI;

public record GetCapabilitiesCommand : ICommand<Result<TorznabCapsResponseDTO>>;

public class GetCapabilitiesCommandValidator : AbstractValidator<GetCapabilitiesCommand>
{
    public GetCapabilitiesCommandValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class GetCapabilitiesCommandHandler : ICommandHandler<GetCapabilitiesCommand, Result<TorznabCapsResponseDTO>>
{
    public Task<Result<TorznabCapsResponseDTO>> ExecuteAsync(
        GetCapabilitiesCommand command,
        CancellationToken cancellationToken
    )
    {
        var response = new TorznabCapsResponseDTO
        {
            Xmlns = new System.Xml.Serialization.XmlSerializerNamespaces([
                new System.Xml.XmlQualifiedName("torznab", "http://torznab.com/schemas/2015/feed"),
            ]),
            Server = new TorznabServer { Version = "1.3", Title = "Reaparr Indexer" },
            Limits = new TorznabLimits { Max = TorznabSearchHelpers.MaxPageSize, Default = 50 },
            Searching = new TorznabSearching
            {
                Search = new TorznabSearch { Available = "yes", SupportedParams = "q,cat,limit,offset,extended,attrs" },
                TvSearch = new TorznabSearch
                {
                    Available = "yes",
                    SupportedParams = "q,season,ep,tvdbid,imdbid,tmdbid,extended,attrs,cat,limit,offset",
                },
                MovieSearch = new TorznabSearch
                {
                    Available = "yes",
                    SupportedParams = "q,imdbid,tmdbid,extended,attrs,cat,limit,offset",
                },
            },
            Categories = IntegrationDefinitions
                .SupportedTorznabCategories.Select(x => new TorznabCategory((int)x.Id, x.Name))
                .ToList(),
        };

        return Task.FromResult(Result.Ok(response));
    }
}
