using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Reaparr.AppHost;

/// <summary>
/// Documents the application's global response headers on each concrete response in an OpenAPI operation.
/// </summary>
/// <remarks>
/// This transformer changes documentation only. The HTTP pipeline remains responsible for emitting the actual
/// headers. Native OpenAPI activates the transformer through dependency injection so the documented version
/// comes from the same build-information service used by the running application.
/// </remarks>
/// <param name="appBuildInfo">The application's build metadata, including the informational version used in header examples.</param>
internal sealed class OpenApiGlobalHeaders(IAppBuildInfo appBuildInfo) : IOpenApiOperationTransformer
{
    /// <summary>
    /// Adds required version and platform header descriptions to all concrete responses declared by an operation.
    /// </summary>
    /// <param name="operation">The operation whose response header definitions are updated in place.</param>
    /// <param name="context">The native OpenAPI operation context; no request or response is modified through it.</param>
    /// <param name="cancellationToken">The pipeline token; building in-memory header metadata is synchronous.</param>
    /// <returns>A completed task, including when the operation declares no response collection.</returns>
    /// <remarks>
    /// Both successful and error responses receive the documentation. Existing response references are left
    /// unchanged, and entries for these global headers on concrete responses are replaced with the canonical
    /// descriptions. The version example is read from the injected build metadata rather than a release literal;
    /// the platform example remains the configured Docker example.
    /// </remarks>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (operation.Responses is null)
            return Task.CompletedTask;

        foreach (var response in operation.Responses.Values)
        {
            if (response is not OpenApiResponse openApiResponse)
                continue;

            openApiResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
            var headers = openApiResponse.Headers!;
            headers[ReaparrHeaders.Version] = CreateHeader(
                appBuildInfo.InformationalVersion,
                "Current Reaparr version"
            );
            headers[ReaparrHeaders.Platform] = CreateHeader("docker", "Current Reaparr platform");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates the required string-header definition shared by the documented global response headers.
    /// </summary>
    /// <param name="example">The illustrative header value, also stored in the schema's examples collection.</param>
    /// <param name="description">The meaning of the header, included on both the header and its string schema.</param>
    /// <returns>A new required OpenAPI header with its example and string-schema metadata populated.</returns>
    /// <remarks>
    /// Header examples and schema examples occupy separate locations in the OpenAPI object model. The header
    /// example is emitted by the current OpenAPI 3.0 document writer, while the schema's examples collection is
    /// a newer JSON Schema keyword and is not emitted by that writer. Neither value sets an actual HTTP header.
    /// </remarks>
    private static OpenApiHeader CreateHeader(string example, string description)
    {
        var exampleNode = JsonValue.Create(example)!;
        return new OpenApiHeader
        {
            Example = exampleNode,
            Required = true,
            Description = description,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Examples = [exampleNode],
                Description = description,
            },
        };
    }
}
