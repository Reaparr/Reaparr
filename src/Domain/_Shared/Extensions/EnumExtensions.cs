using System.Reflection;

namespace Reaparr.Domain;

public static class EnumExtensions
{
    public static string ToEnumMemberValue(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();

        return member?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
            ?? member?.GetCustomAttribute<EnumMemberAttribute>()?.Value
            ?? value.ToString();
    }

    public static int ToComparisonId(this PlexMediaComparisonState value) => (int)value;

    public static PlexMediaComparisonState ToComparisonState(this int value) =>
        Enum.IsDefined(typeof(PlexMediaComparisonState), value)
            ? (PlexMediaComparisonState)value
            : PlexMediaComparisonState.Unknown;
}
