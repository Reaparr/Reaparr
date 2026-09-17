namespace Reaparr.Data.Configurations;

public class PlexTvShowConfiguration : IEntityTypeConfiguration<PlexTvShow>
{
    public void Configure(EntityTypeBuilder<PlexTvShow> builder)
    {
        builder.HasIndex(x => x.SortIndex);
        builder.HasIndex(x => x.Quality);
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasIndex(x => new { x.PlexLibraryId, x.Quality });
        // SearchTvShowCommandHandler and SearchGenericCommandHandler use LIKE '<normalized-title>%';
        // NOCASE lets SQLite turn that case-insensitive prefix predicate into an index range.
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => x.SearchTitle);

        // SearchTvShowCommandHandler and SearchGenericCommandHandler first restrict shows by accessible library,
        // then match Guid_TVDB; this index resolves show candidates before episode and media-data rows are read.
        builder.HasIndex(x => new { x.PlexLibraryId, x.Guid_TVDB });

        // SearchTvShowCommandHandler and SearchGenericCommandHandler first restrict shows by accessible library,
        // then match Guid_TMDB; this index resolves show candidates before episode and media-data rows are read.
        builder.HasIndex(x => new { x.PlexLibraryId, x.Guid_TMDB });

        // SearchTvShowCommandHandler and SearchGenericCommandHandler first restrict shows by accessible library,
        // then match Guid_IMDB; this index resolves show candidates before episode and media-data rows are read.
        builder.HasIndex(x => new { x.PlexLibraryId, x.Guid_IMDB });

        // SearchTvShowCommandHandler and SearchGenericCommandHandler first restrict shows by accessible library,
        // then run a normalized SearchTitle prefix match; this index avoids scanning every show in those libraries.
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });

        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });

        builder
            .HasMany(x => x.Actors)
            .WithMany(x => x.PlexTvShowActors)
            .UsingEntity<PlexTvShowActors>(
                l => l.HasOne<PlexActor>().WithMany().HasForeignKey(e => e.PlexActorId),
                r => r.HasOne<PlexTvShow>().WithMany().HasForeignKey(e => e.PlexTvShowId)
            );

        builder
            .HasMany(x => x.Genres)
            .WithMany(x => x.PlexTvShowGenres)
            .UsingEntity<PlexTvShowGenres>(
                l => l.HasOne<PlexGenre>().WithMany().HasForeignKey(e => e.GenresId),
                r => r.HasOne<PlexTvShow>().WithMany().HasForeignKey(e => e.PlexTvShowId)
            );

        builder
            .HasMany(x => x.Countries)
            .WithMany(x => x.PlexTvShowCountries)
            .UsingEntity<PlexTvShowCountries>(
                l => l.HasOne<PlexCountry>().WithMany().HasForeignKey(e => e.CountryId),
                r => r.HasOne<PlexTvShow>().WithMany().HasForeignKey(e => e.PlexTvShowId)
            );

        builder
            .HasMany(x => x.Qualities)
            .WithOne(x => x.PlexTvShow)
            .HasForeignKey(x => x.PlexTvShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
