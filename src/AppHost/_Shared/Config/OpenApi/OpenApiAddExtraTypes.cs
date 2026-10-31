using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Reaparr.Application.Contracts;
using Reaparr.SignalR.Contracts;

namespace Reaparr.AppHost;

/// <summary>
/// Adds DTOs used by the generated client but not referenced by an endpoint schema.
/// </summary>
/// <remarks>
/// Registers contracts used outside HTTP endpoint signatures, such as SignalR messages and download updates,
/// so API generation can still produce their named types. The same instance must participate in both schema
/// and document transformation: the schema pass captures serialization metadata, and the document pass uses
/// that metadata to promote nested contracts into reusable component references.
/// </remarks>
internal sealed class OpenApiAddExtraTypes(Func<JsonTypeInfo, string?> createSchemaReferenceId)
    : IOpenApiDocumentTransformer,
        IOpenApiSchemaTransformer
{
    private readonly ConditionalWeakTable<OpenApiSchema, JsonTypeInfo> _schemaTypes = new();

    private static readonly Type[] ExtraTypes =
    [
        typeof(MessageTypes),
        typeof(JobTypes),
        typeof(JobStatus),
        typeof(RefreshDataType),
        typeof(DownloadActions),
        typeof(SortDirection),
        typeof(SortType),
        typeof(Sort),
        typeof(NotificationDTO),
        typeof(DesktopMessageDTO),
        typeof(ServerDownloadProgressDTO),
        typeof(DownloadProgressDTO),
        typeof(ServerDownloadProgressMessagePackDTO),
        typeof(DownloadProgressMessagePackDTO),
        typeof(ServerConnectionCheckStatusProgressDTO),
        typeof(MediaQueryFilterDTO),
        typeof(CheckAllConnectionStatusUpdateDTO),
        typeof(DownloadJobUpdateDTO),
        typeof(InspectPlexServerJobUpdateDTO),
        typeof(MoveDownloadFileJobUpdateDTO),
        typeof(LibrarySyncJobQueueDTO),
        typeof(LibrarySyncProgressDTO),
        typeof(DownloadPatchDTO),
        typeof(DownloadPatchMessagePackDTO),
        typeof(AppUpdateDownloadProgressDTO),
        typeof(LibraryComparisonCompletedDTO),
        typeof(IntegrationSetupProgressDTO),
    ];

    /// <summary>
    /// Associates a generated concrete schema with the JSON type metadata that produced it.
    /// </summary>
    /// <param name="schema">The schema instance whose originating CLR type must remain discoverable.</param>
    /// <param name="context">The generation context supplying the type's configured serialization metadata.</param>
    /// <param name="cancellationToken">The pipeline token; this synchronous metadata capture performs no cancellable I/O.</param>
    /// <returns>A completed task after recording the schema-to-type association.</returns>
    /// <remarks>
    /// This pass does not modify the serialized schema. The weak-key association lets the later document pass
    /// recover names for nested contracts without embedding CLR metadata in the OpenAPI document or keeping
    /// discarded schema instances alive solely for this lookup.
    /// </remarks>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        _schemaTypes.AddOrUpdate(schema, context.JsonTypeInfo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds client-only contracts and their reachable named child schemas to the completed document.
    /// </summary>
    /// <param name="document">The document whose component schema collection is created or extended in place.</param>
    /// <param name="context">The document context used to generate schemas with the registered JSON options and transformers.</param>
    /// <param name="cancellationToken">The pipeline token forwarded to each asynchronous schema-generation request.</param>
    /// <returns>A task that completes after all missing extra contracts have been generated and registered.</returns>
    /// <remarks>
    /// Existing root components are retained. Missing roots use their CLR type names, while nested contracts
    /// follow the configured reference-name policy. Registering nested components is necessary for generated
    /// clients to share enum and DTO identities rather than receiving unrelated inline copies of those types.
    /// </remarks>
    public async Task TransformAsync(
        Microsoft.OpenApi.OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        foreach (var type in ExtraTypes)
        {
            if (document.Components.Schemas.ContainsKey(type.Name))
                continue;

            var schema = await context.GetOrCreateSchemaAsync(type, cancellationToken: cancellationToken);
            RegisterSchema(document, schema, type.Name);
        }
    }

    /// <summary>
    /// Registers a concrete schema under a component name when one is available and normalizes its child references.
    /// </summary>
    /// <param name="document">The owning document, with its component schema collection already initialized.</param>
    /// <param name="schema">A concrete schema to register, or an existing reference to preserve unchanged.</param>
    /// <param name="name">
    /// An explicit root component name. When omitted, captured JSON metadata and the document's naming policy
    /// determine whether the schema can be named; schemas without a name remain inline.
    /// </param>
    /// <returns>
    /// A document-bound reference for a named component, the normalized concrete schema for an inline component,
    /// or the original value when it is not a concrete schema.
    /// </returns>
    /// <remarks>
    /// A component already registered under the selected name is reused. New named components are inserted
    /// before descending into their children, allowing recursive contracts to refer back to an existing
    /// component instead of repeatedly expanding it. Child schemas are normalized in place.
    /// </remarks>
    private IOpenApiSchema RegisterSchema(
        Microsoft.OpenApi.OpenApiDocument document,
        IOpenApiSchema schema,
        string? name = null
    )
    {
        if (schema is not OpenApiSchema concreteSchema)
            return schema;

        if (name is null && _schemaTypes.TryGetValue(concreteSchema, out var typeInfo))
            name = createSchemaReferenceId(typeInfo);

        var schemas = document.Components!.Schemas!;
        if (name is not null)
        {
            if (schemas.ContainsKey(name))
                return new OpenApiSchemaReference(name, document);

            schemas[name] = concreteSchema;
        }

        RegisterChildSchemas(document, concreteSchema);

        return name is null ? concreteSchema : new OpenApiSchemaReference(name, document);
    }

    /// <summary>
    /// Walks the child-schema locations supported by this registrar and replaces named children with component references.
    /// </summary>
    /// <param name="document">The document that owns any components registered during traversal.</param>
    /// <param name="schema">The concrete parent schema whose child slots are updated in place.</param>
    /// <remarks>
    /// Traversal covers object properties, array items, additional-property values, negation, and the allOf,
    /// anyOf, and oneOf composition lists. Each child goes through the same registration rules as a root;
    /// existing references are retained, while unnamed schemas remain inline and have their own children visited.
    /// </remarks>
    private void RegisterChildSchemas(Microsoft.OpenApi.OpenApiDocument document, OpenApiSchema schema)
    {
        if (schema.Properties is { } properties)
            foreach (var (key, property) in properties)
                properties[key] = RegisterSchema(document, property);
        if (schema.Items is { } items)
            schema.Items = RegisterSchema(document, items);
        if (schema.AdditionalProperties is { } additionalProperties)
            schema.AdditionalProperties = RegisterSchema(document, additionalProperties);
        if (schema.Not is { } not)
            schema.Not = RegisterSchema(document, not);
        RegisterSchemas(document, schema.AllOf);
        RegisterSchemas(document, schema.AnyOf);
        RegisterSchemas(document, schema.OneOf);
    }

    /// <summary>
    /// Normalizes every member of an optional schema-composition list using the document's component registry.
    /// </summary>
    /// <param name="document">The document receiving named components discovered in the list.</param>
    /// <param name="schemas">An allOf, anyOf, or oneOf list to update in place; null means there is no list to visit.</param>
    /// <remarks>
    /// Entries retain their order and position. Only the schema value occupying a slot can change, for example
    /// from a concrete nested DTO schema to a reference to its shared component definition.
    /// </remarks>
    private void RegisterSchemas(OpenApiDocument document, IList<IOpenApiSchema>? schemas)
    {
        if (schemas is null)
            return;

        for (var index = 0; index < schemas.Count; index++)
            schemas[index] = RegisterSchema(document, schemas[index]);
    }
}
