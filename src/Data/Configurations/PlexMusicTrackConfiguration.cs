namespace Reaparr.Data;

public class PlexMusicTrackConfiguration : IEntityTypeConfiguration<PlexMusicTrack>
{
    public void Configure(EntityTypeBuilder<PlexMusicTrack> builder)
    {
        builder.Ignore(x => x.VideoQuality);
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexAlbumId, x.DiscNumber, x.TrackNumber, x.Id });
        builder.HasAlternateKey(x => new { x.PlexLibraryId, x.Id });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.Property(x => x.MusicBrainzReleaseTrackId).UseCollation("BINARY");
        builder.Property(x => x.PlexApiIdentityId).UseCollation("BINARY");
        builder.HasIndex(x => new { x.PlexLibraryId, PlexIdentityId = x.PlexApiIdentityId });

        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.Tracks)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.PlexAlbum)
            .WithMany(x => x.Tracks)
            .HasForeignKey(x => new { x.PlexLibraryId, x.PlexAlbumId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
