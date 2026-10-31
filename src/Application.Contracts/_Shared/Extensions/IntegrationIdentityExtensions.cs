namespace Reaparr.Application.Contracts;

public static class IntegrationIdentityExtensions
{
    public static IntegrationIdentity ToSonarrIdentity(this Guid integrationId) =>
        new(IntegrationType.Sonarr, integrationId);

    public static IntegrationIdentity ToRadarrIdentity(this Guid integrationId) =>
        new(IntegrationType.Radarr, integrationId);

    public static bool Supports(this IntegrationIdentity identity, PlexMediaType mediaType) =>
        identity.Type switch
        {
            IntegrationType.Sonarr => mediaType == PlexMediaType.Episode,
            IntegrationType.Radarr => mediaType == PlexMediaType.Movie,
            _ => false,
        };

    public static IntegrationIdentity? ToIntegrationIdentity(
        this (Guid? SonarrIntegrationId, Guid? RadarrIntegrationId) integrationIds
    )
    {
        if (integrationIds.SonarrIntegrationId is not null && integrationIds.RadarrIntegrationId is not null)
            throw new InvalidOperationException(
                "A download task cannot belong to both Sonarr and Radarr integrations."
            );

        if (integrationIds.SonarrIntegrationId is not null)
            return integrationIds.SonarrIntegrationId.Value.ToSonarrIdentity();

        return integrationIds.RadarrIntegrationId?.ToRadarrIdentity();
    }
}
