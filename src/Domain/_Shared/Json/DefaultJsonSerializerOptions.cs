using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Reaparr.Domain;

public static class DefaultJsonSerializerOptions
{
    /// <summary>
    /// Disable throwing exceptions when a required property is missing, this will be defaulted to the default value.
    /// Otherwise, we need separate models for serialization/deserialization and DTO's.
    /// Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties
    /// </summary>
    private static void DisableThrowingExceptionsWhenRequiredPropertyIsMissing(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var propertyInfo in typeInfo.Properties)
            // Strip IsRequired constraint from every property.
            propertyInfo.IsRequired = false;
    }

    /// <summary>
    /// If a property is missing, and would normally set null, then skip setting the property.
    /// Source: https://stackoverflow.com/a/77520195/8205497
    /// </summary>
    /// <param name="typeInfo"></param>
    private static void InterceptNullSetter(JsonTypeInfo typeInfo)
    {
        foreach (var propertyInfo in typeInfo.Properties)
        {
            var setProperty = propertyInfo.Set;
            if (setProperty is not null)
            {
                propertyInfo.Set = (obj, value) =>
                {
                    if (value != null)
                    {
                        //value = ((Demo)obj).B + value;
                        setProperty(obj, value);
                    }
                };
            }
        }
    }

    /// <summary>
    /// Creates a fresh serializer configuration using the same standard policy applied to HTTP JSON options.
    /// </summary>
    /// <returns>A new, mutable options instance with the application's standard JSON configuration applied.</returns>
    private static JsonSerializerOptions CreateStandardOptions() =>
        new JsonSerializerOptions().ApplyStandardConfiguration();

    /// <summary>
    /// Applies the application's standard JSON policy directly to an existing serializer options instance.
    /// </summary>
    /// <param name="options">The non-null, mutable options instance to configure before its first serialization use.</param>
    /// <returns>The same options instance, allowing the shared policy to be used by factories and host configuration callbacks.</returns>
    /// <exception cref="InvalidOperationException">The supplied options have already become read-only.</exception>
    /// <remarks>
    /// <para>
    /// The policy uses camel-case property names, case-insensitive property reads, string enum conversion, and
    /// omission of null properties when writing JSON. Enum-specific converters and member-name annotations
    /// remain responsible for custom wire values such as the lower-case sorting identifiers.
    /// </para>
    /// <para>
    /// The configured reflection resolver also ignores explicit null setter values and relaxes missing required
    /// properties during deserialization. Applying the resolver here keeps HTTP behavior aligned with the shared
    /// preset; copying only converters and naming options would omit these metadata modifiers. OpenAPI's
    /// required-property transformer independently describes the client-facing nullability contract.
    /// </para>
    /// <para>
    /// Configuration mutates rather than replaces the supplied instance, which is necessary for host-owned
    /// options exposed through a get-only property. It replaces the resolver and appends the standard enum
    /// converter, so callers should apply it once during initial configuration, not after caching type metadata.
    /// Unassigned framework options retain their existing values.
    /// </para>
    /// </remarks>
    public static JsonSerializerOptions ApplyStandardConfiguration(this JsonSerializerOptions options)
    {
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new JsonStringEnumConverter());
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { InterceptNullSetter, DisableThrowingExceptionsWhenRequiredPropertyIsMissing },
        };
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        return options;
    }

    private static readonly Lazy<JsonSerializerOptions> _configStandardField = new(CreateStandardOptions);

    private static readonly Lazy<JsonSerializerOptions> _configCapitalizedField = new(() =>
    {
        var options = CreateStandardOptions();
        options.PropertyNamingPolicy = null;
        return options;
    });

    private static readonly Lazy<JsonSerializerOptions> _configPlexApiSerializationField = new(() =>
    {
        var options = CreateStandardOptions();
        options.PropertyNamingPolicy = null;
        options.Converters.Clear();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        return options;
    });

    private static readonly Lazy<JsonSerializerOptions> _configManagerOptionsField = new(() =>
    {
        var options = CreateStandardOptions();

        //If Model.property is collect and this config is Populate,then skip propertyInfo.Set
        options.PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate;
        options.WriteIndented = true;
        options.PropertyNamingPolicy = null;

        return options;
    });

    public static JsonSerializerOptions ConfigStandard => _configStandardField.Value;

    public static JsonSerializerOptions ConfigCapitalized => _configCapitalizedField.Value;

    public static JsonSerializerOptions UserSettingsOptions => _configManagerOptionsField.Value;

    public static JsonSerializerOptions PlexApiSerialization => _configPlexApiSerializationField.Value;
}
