using System.Runtime.CompilerServices;
using System.Text;

namespace Reaparr.Data.Contracts;

public static partial class DbContextExtensions
{
    private static readonly IEqualityComparer<(string LanguageCode, string LanguageTag)> _streamLanguageKeyComparer =
        EqualityComparer<(string LanguageCode, string LanguageTag)>.Create(
            static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Equals(left.LanguageCode, right.LanguageCode)
                && StringComparer.OrdinalIgnoreCase.Equals(left.LanguageTag, right.LanguageTag),
            static language =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(language.LanguageCode),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(language.LanguageTag)
                )
        );

    /// <summary>
    /// Inserts normalized language pairs when absent and returns a case-insensitive lookup of their identities.
    /// The unique database index arbitrates concurrent inserts.
    /// </summary>
    public static async Task<
        Dictionary<(string LanguageCode, string LanguageTag), int>
    > InsertOrIgnoreStreamLanguagesAsync(
        this IReaparrDbContext dbContext,
        IEnumerable<StreamLanguage> languages,
        CancellationToken cancellationToken
    )
    {
        var pairs = languages
            .Select(x =>
                (LanguageCode: x.LanguageCode.ToLowerInvariant(), LanguageTag: x.LanguageTag.ToLowerInvariant())
            )
            .Distinct();
        var result = new Dictionary<(string LanguageCode, string LanguageTag), int>(_streamLanguageKeyComparer);

        // Two parameters per pair: both writes and reads remain below SQLite's conservative 999 limit.
        foreach (var chunk in pairs.Chunk(400))
        {
            var arguments = new object[chunk.Length * 2];
            var insert = new StringBuilder(
                $"INSERT OR IGNORE INTO {nameof(IReaparrDbContext.StreamLanguages)} ({nameof(StreamLanguage.LanguageCode)}, {nameof(StreamLanguage.LanguageTag)}) VALUES "
            );
            var select = new StringBuilder(
                $"SELECT {nameof(StreamLanguage.Id)}, {nameof(StreamLanguage.LanguageCode)}, {nameof(StreamLanguage.LanguageTag)} FROM {nameof(IReaparrDbContext.StreamLanguages)} WHERE "
            );
            for (var index = 0; index < chunk.Length; index++)
            {
                if (index > 0)
                {
                    insert.Append(", ");
                    select.Append(" OR ");
                }

                var codeParameter = index * 2;
                var tagParameter = codeParameter + 1;
                arguments[codeParameter] = chunk[index].LanguageCode;
                arguments[tagParameter] = chunk[index].LanguageTag;
                insert.Append("({").Append(codeParameter).Append("}, {").Append(tagParameter).Append("})");
                select
                    .Append($"({nameof(StreamLanguage.LanguageCode)} = {{")
                    .Append(codeParameter)
                    .Append("} AND ")
                    .Append(nameof(StreamLanguage.LanguageTag))
                    .Append(" = {")
                    .Append(tagParameter)
                    .Append("})");
            }

            await dbContext.ExecuteSqlInterpolatedAsync(
                FormattableStringFactory.Create(insert.ToString(), arguments),
                cancellationToken
            );
            var found = await dbContext
                .StreamLanguages.FromSqlInterpolated(FormattableStringFactory.Create(select.ToString(), arguments))
                .Select(x => new
                {
                    x.Id,
                    x.LanguageCode,
                    x.LanguageTag,
                })
                .ToListAsync(cancellationToken);
            foreach (var language in found)
                result.Add((language.LanguageCode, language.LanguageTag), language.Id);
        }

        return result;
    }
}
