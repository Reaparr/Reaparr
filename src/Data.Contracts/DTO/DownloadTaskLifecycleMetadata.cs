namespace Reaparr.Data.Contracts;

public record DownloadTaskLifecycleMetadata
{
    public required DateTime CreatedAt { get; init; }

    public Guid? SonarrIntegrationId { get; init; }

    public Guid? RadarrIntegrationId { get; init; }
}