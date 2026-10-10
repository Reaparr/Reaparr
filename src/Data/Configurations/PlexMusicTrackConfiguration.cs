namespace Reaparr.Data;

public class PlexMusicTrackConfiguration : IEntityTypeConfiguration<PlexMusicTrack>
{
    public void Configure(EntityTypeBuilder<PlexMusicTrack> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
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
            .HasForeignKey(x => x.PlexAlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
