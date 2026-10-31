namespace Reaparr.Data;

public class PlexMusicTrackComparisonConfiguration : IEntityTypeConfiguration<PlexMusicTrackComparison>
{
    public void Configure(EntityTypeBuilder<PlexMusicTrackComparison> builder)
    {
        builder.Property(x => x.HitState).HasDefaultValue(PlexMediaComparisonHitState.Matched);
        builder.Property(x => x.RemoteAudioQuality).HasDefaultValue(AudioQuality.Unknown);
        builder.Property(x => x.OwnedAudioQuality).HasDefaultValue(AudioQuality.Unknown);
        builder.HasIndex(x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId });
        builder.HasIndex(x => x.RemotePlexMediaDataId);
        builder.HasIndex(x => x.OwnedPlexMediaDataId);
        builder
            .HasIndex(x => new
            {
                x.RemotePlexLibraryId,
                x.OwnedPlexLibraryId,
                x.RemotePlexMediaId,
                x.OwnedPlexMediaId,
            })
            .IsUnique();
        builder
            .HasOne<PlexLibrary>()
            .WithMany()
            .HasForeignKey(x => x.RemotePlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexLibrary>()
            .WithMany()
            .HasForeignKey(x => x.OwnedPlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexMusicTrack>()
            .WithMany()
            .HasForeignKey(x => new { x.RemotePlexLibraryId, x.RemotePlexMediaId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexMusicTrack>()
            .WithMany()
            .HasForeignKey(x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexMusicTrackMediaData>()
            .WithMany()
            .HasForeignKey(x => x.RemotePlexMediaDataId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne<PlexMusicTrackMediaData>()
            .WithMany()
            .HasForeignKey(x => x.OwnedPlexMediaDataId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
