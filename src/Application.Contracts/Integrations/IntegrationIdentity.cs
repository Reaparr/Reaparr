namespace Reaparr.Application.Contracts;

/// <summary>Identifies an integration by its provider and stored integration record ID.</summary>
/// <param name="Type">Provider owning the integration, currently Sonarr or Radarr.</param>
/// <param name="Id">GUID of the stored integration record.</param>
public record IntegrationIdentity(IntegrationType Type, Guid Id);
