using System.Text.Json;

namespace Reaparr.Data;

public static class EntityConfigurationsExtensions
{
    public static PropertyBuilder<TProperty> HasJsonConversion<TProperty>(this PropertyBuilder<TProperty> builder)
    {
        return builder.HasConversion(
            x => JsonSerializer.Serialize(x, DefaultJsonSerializerOptions.ConfigStandard),
            x => JsonSerializer.Deserialize<TProperty>(x, DefaultJsonSerializerOptions.ConfigStandard)!
        );
    }
}
