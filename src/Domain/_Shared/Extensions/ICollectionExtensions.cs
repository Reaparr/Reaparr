namespace Reaparr.Domain;

// ReSharper disable once InconsistentNaming
public static class ICollectionExtensions
{
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        if (collection is null)
            throw new ArgumentNullException(nameof(collection));

        if (items is null)
            throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
