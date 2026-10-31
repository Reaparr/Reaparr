namespace Reaparr.Data;

public class PlexMusicAlbumMediaQualityConfiguration : IEntityTypeConfiguration<PlexMusicAlbumMediaQuality>
{
    public void Configure(EntityTypeBuilder<PlexMusicAlbumMediaQuality> builder)
    {
        builder
            .HasIndex(x => new
            {
                x.PlexLibraryId,
                x.PlexMusicAlbumId,
                Quality = x.AudioQuality,
            })
            .IsUnique();
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany()
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexMusicAlbum)
            .WithMany(x => x.Qualities)
            .HasForeignKey(x => new { x.PlexLibraryId, x.PlexMusicAlbumId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
