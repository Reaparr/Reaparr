using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Reaparr.AppHost;

/// <summary>
/// Preserves the former NSwag behavior of marking non-nullable DTO properties as required.
/// </summary>
internal sealed class OpenApiRequiredPropertiesSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Reconciles an object schema's required-property set with CLR nullability and explicit validation requirements.
    /// </summary>
    /// <param name="schema">The schema whose required-property names are added or removed in place.</param>
    /// <param name="context">The JSON metadata supplying serialized property names, CLR types, and validation attributes.</param>
    /// <param name="cancellationToken">The pipeline token; reconciling this in-memory metadata is synchronous.</param>
    /// <returns>A completed task, including when the schema has no object properties to reconcile.</returns>
    /// <remarks>
    /// Only properties present in both JSON metadata and the generated schema are considered. Non-nullable
    /// properties and properties carrying <see cref="RequiredAttribute"/> are required; nullable properties
    /// without that attribute are removed from the required set. This maintains the generated client contract
    /// independently of the shared deserialization resolver, which deliberately relaxes missing-field checks
    /// at runtime. It does not change the property's schema type or perform request validation.
    /// </remarks>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            if (!schema.Properties.ContainsKey(property.Name))
                continue;

            if (IsNullable(property) && property.AttributeProvider?.IsDefined(typeof(RequiredAttribute), true) != true)
                schema.Required?.Remove(property.Name);
            else
            {
                schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                schema.Required.Add(property.Name);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether a JSON property's CLR value can be null for purposes of the generated response contract.
    /// </summary>
    /// <param name="property">The configured JSON property metadata to inspect.</param>
    /// <returns>
    /// True for nullable value types or nullable reference-type getters; false for non-nullable value types
    /// and reference-type getters whose metadata does not permit null.
    /// </returns>
    /// <remarks>
    /// Value types require inspection of their nullable wrapper rather than the getter's reference-nullability
    /// flag. Explicit required-validation attributes are handled by the caller and do not alter this
    /// nullability classification.
    /// </remarks>
    private static bool IsNullable(JsonPropertyInfo property) =>
        property.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(property.PropertyType) is not null
            : property.IsGetNullable;
}
