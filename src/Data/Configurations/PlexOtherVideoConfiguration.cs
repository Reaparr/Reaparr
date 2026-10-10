namespace Reaparr.Data;

public class PlexOtherVideoConfiguration : IEntityTypeConfiguration<PlexOtherVideo>
{
    public void Configure(EntityTypeBuilder<PlexOtherVideo> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.HasOne(x => x.PlexLibrary).WithMany(x => x.OtherVideos).HasForeignKey(x => x.PlexLibraryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PlexServer).WithMany().HasForeignKey(x => x.PlexServerId).OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Actors)
            .WithMany(x => x.PlexOtherVideoActors)
            .UsingEntity<PlexOtherVideoActors>(
                l => l.HasOne<PlexActor>().WithMany().HasForeignKey(e => e.PlexActorId),
                r => r.HasOne<PlexOtherVideo>().WithMany().HasForeignKey(e => e.PlexOtherVideoId)
            );

        builder
            .HasMany(x => x.Genres)
            .WithMany(x => x.PlexOtherVideoGenres)
            .UsingEntity<PlexOtherVideoGenres>(
                l => l.HasOne<PlexGenre>().WithMany().HasForeignKey(e => e.GenresId),
                r => r.HasOne<PlexOtherVideo>().WithMany().HasForeignKey(e => e.PlexOtherVideoId)
            );

        builder
            .HasMany(x => x.Countries)
            .WithMany(x => x.PlexOtherVideoCountries)
            .UsingEntity<PlexOtherVideoCountries>(
                l => l.HasOne<PlexCountry>().WithMany().HasForeignKey(e => e.CountryId),
                r => r.HasOne<PlexOtherVideo>().WithMany().HasForeignKey(e => e.PlexOtherVideoId)
            );
    }
}
