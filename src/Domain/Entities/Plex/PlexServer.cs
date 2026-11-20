namespace Reaparr.Domain;

public class PlexServer : BaseEntity
{
    #region Properties

    /// <summary>
    /// Gets or sets the name of this <see cref="PlexServer"/>.
    /// </summary>
    [Column(Order = 1)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the id Plex has assigned to the PlexAccount.
    /// </summary>
    [Column(Order = 2)]
    public required long OwnerId { get; init; }

    /// <summary>
    /// Gets or sets what seems like the username of the Plex server owner.
    /// Mapped from "sourceTitle".
    /// </summary>
    [Column(Order = 3)]
    public required string PlexServerOwnerUsername { get; init; }

    /// <summary>
    /// Gets or sets the type of hardware operating system this <see cref="PlexServer"/> is running.
    /// </summary>
    [Column(Order = 4)]
    public required string Device { get; init; }

    /// <summary>
    /// Gets or sets the hardware operating system this <see cref="PlexServer"/> is running.
    /// </summary>
    [Column(Order = 5)]
    public required string Platform { get; init; }

    /// <summary>
    /// Gets or sets the hardware operating system version this <see cref="PlexServer"/> is running.
    /// </summary>
    [Column(Order = 6)]
    public required string PlatformVersion { get; init; }

    /// <summary>
    /// Gets or sets the Plex software this <see cref="PlexServer"/> is running.
    /// </summary>
    [Column(Order = 7)]
    public required string Product { get; init; }

    /// <summary>
    /// Gets or sets the Plex software version this <see cref="PlexServer"/> is running.
    /// </summary>
    [Column(Order = 8)]
    public required string ProductVersion { get; init; }

    /// <summary>
    /// Gets or sets the role this <see cref="PlexServer"/> provides, seems to be mostly "server".
    /// </summary>
    [Column(Order = 9)]
    public required string Provides { get; init; }

    [Column(Order = 10)]
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets the last time this server has been online based on what Plex has seen.
    /// </summary>
    [Column(Order = 11)]
    public required DateTime LastSeenAt { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier for this server. This is mapped from the new Plex clientId.
    /// </summary>
    [Column(Order = 12)]
    public required string MachineIdentifier { get; set; }

    [Column(Order = 13)]
    public required string PublicAddress { get; init; }

    [Column(Order = 14)]
    public required int PreferredConnectionId { get; set; }

    [Column(Order = 15)]
    public required bool IsEnabled { get; set; } = true;

    [Column(Order = 16)]
    public required bool IsDownloadsPausedByUser { get; set; }

    [Column(Order = 17)]
    public bool? OwnedOverride { get; set; }

    [Column(Order = 18)]
    public required bool Synced { get; init; }

    [Column(Order = 19)]
    public required bool Relay { get; init; }

    [Column(Order = 20)]
    public required bool Presence { get; init; }

    [Column(Order = 21)]
    public required bool HttpsRequired { get; init; }

    [Column(Order = 22)]
    public required bool PublicAddressMatches { get; init; }

    [Column(Order = 23)]
    public required bool DnsRebindingProtection { get; init; }

    [Column(Order = 24)]
    public required bool NatLoopbackSupported { get; init; }

    #endregion

    #region Relationships

    public required ICollection<PlexAccountServer> PlexAccountServers { get; init; } = [];

    public required ICollection<PlexLibrary> PlexLibraries { get; init; } = [];

    public required ICollection<PlexServerStatus> ServerStatus { get; init; } = [];

    /// <summary>
    /// Gets or sets the different connections that can be used to communicate with the <see cref="PlexServer"/>.
    /// </summary>
    public required ICollection<PlexServerConnection> PlexServerConnections { get; set; } = [];

    #endregion

    #region Helpers

    /// <summary>
    /// Gets whether this server is marked Owned by any Plex account or overridden.
    /// </summary>
    [NotMapped]
    public bool Owned => OwnedOverride ?? PlexAccountServers.Any(x => x.IsServerOwned);

    /// <summary>
    /// Gets the last known server status.
    /// </summary>
    [NotMapped]
    public PlexServerStatus Status
    {
        get
        {
            if (ServerStatus.Any())
                return ServerStatus.Last();

            // TODO:Add initial server status when server is added to DB. Meaning there is always one.
            return new PlexServerStatus
            {
                Id = 0,
                IsSuccessful = false,
                PlexServer = this,
                StatusMessage = "Not checked yet",
                PlexServerId = Id,
                StatusCode = 0,
                LastChecked = default,
                PlexServerConnectionId = 0,
            };
        }
    }

    #endregion

    /// <inheritdoc/>
    [SuppressMessage(
        "ReSharper",
        "NonReadonlyMemberInGetHashCode",
        Justification = "Cannot be ReadOnly due to usage in Entity Framework as Entities"
    )]
    public override int GetHashCode()
    {
        var hashCode = default(HashCode);
        hashCode.Add(Name);
        hashCode.Add(OwnerId);
        hashCode.Add(PlexServerOwnerUsername);
        hashCode.Add(Device);
        hashCode.Add(Platform);
        hashCode.Add(PlatformVersion);
        hashCode.Add(Product);
        hashCode.Add(ProductVersion);
        hashCode.Add(Provides);
        hashCode.Add(CreatedAt);
        hashCode.Add(LastSeenAt);
        hashCode.Add(MachineIdentifier);
        hashCode.Add(PublicAddress);
        hashCode.Add(PreferredConnectionId);
        hashCode.Add(OwnedOverride);
        hashCode.Add(Synced);
        hashCode.Add(Relay);
        hashCode.Add(Presence);
        hashCode.Add(HttpsRequired);
        hashCode.Add(PublicAddressMatches);
        hashCode.Add(DnsRebindingProtection);
        hashCode.Add(NatLoopbackSupported);
        return hashCode.ToHashCode();
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        return obj.GetType() == GetType() && Equals((PlexServer)obj);
    }

    /// <summary>
    /// Compares this <see cref="PlexServer"/> against another <see cref="PlexServer"/> without comparing the Id and navigation properties.
    /// </summary>
    /// <param name="other">The other <see cref="PlexServer"/> to compare against.</param>
    /// <returns>Returns whether these <see cref="PlexServer">PlexServers</see> are equal.</returns>
    protected bool Equals(PlexServer other) =>
        Name == other.Name
        && OwnerId == other.OwnerId
        && PlexServerOwnerUsername == other.PlexServerOwnerUsername
        && Device == other.Device
        && Platform == other.Platform
        && PlatformVersion == other.PlatformVersion
        && Product == other.Product
        && ProductVersion == other.ProductVersion
        && Provides == other.Provides
        && CreatedAt.Equals(other.CreatedAt)
        && LastSeenAt.Equals(other.LastSeenAt)
        && MachineIdentifier == other.MachineIdentifier
        && PublicAddress == other.PublicAddress
        && PreferredConnectionId == other.PreferredConnectionId
        && OwnedOverride == other.OwnedOverride
        && Synced == other.Synced
        && Relay == other.Relay
        && Presence == other.Presence
        && HttpsRequired == other.HttpsRequired
        && PublicAddressMatches == other.PublicAddressMatches
        && DnsRebindingProtection == other.DnsRebindingProtection
        && NatLoopbackSupported == other.NatLoopbackSupported;
}
