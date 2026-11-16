using System.Collections.Concurrent;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Builder;

namespace Reaparr.Domain;

public static class FastEndpointsExtensions
{
    private static readonly ConcurrentDictionary<Type, XmlSerializer> _serializerCache = new();

    /// <summary>
    /// Marks an endpoint as internal-only for documentation and discovery purposes.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder to decorate.</param>
    /// <returns>The same builder to allow fluent configuration.</returns>
    public static TBuilder IsInternalApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithGroupName("Internal API");
        return builder;
    }

    /// <summary>
    /// Marks an endpoint as public-facing for documentation and discovery purposes.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder to decorate.</param>
    /// <returns>The same builder to allow fluent configuration.</returns>
    public static TBuilder IsPublicApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithGroupName("Public API");
        return builder;
    }

    /// <summary>
    /// Adds the "Download Client" tag to the endpoint for grouping in documentation/UI.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder to decorate.</param>
    /// <returns>The same builder to allow fluent configuration.</returns>
    public static TBuilder IsDownloadClient<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithTags("Download Client");
        return builder;
    }

    /// <summary>
    /// Adds the "Indexer" tag to the endpoint for grouping in documentation/UI.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder to decorate.</param>
    /// <returns>The same builder to allow fluent configuration.</returns>
    public static TBuilder IsIndexer<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithTags("Indexer");
        return builder;
    }

    /// <summary>
    /// Sends an XML response asynchronously using a cached <see cref="XmlSerializer"/>.
    /// </summary>
    /// <typeparam name="TResponse">The response type to serialize.</typeparam>
    /// <param name="ep">The response sender associated with the current endpoint.</param>
    /// <param name="response">The response-object to serialize to XML.</param>
    /// <param name="statusCode">The HTTP status code to set on the response.</param>
    /// <param name="contentType">The response content type. Defaults to "application/xml".</param>
    /// <param name="cancellationToken">Token to observe while awaiting the copy operation.</param>
    public static async Task XmlAsync<TResponse>(
        this IResponseSender ep,
        TResponse response,
        int statusCode = 200,
        string contentType = "application/xml",
        CancellationToken cancellationToken = default
    )
    {
        ep.HttpContext.MarkResponseStart();
        ep.HttpContext.Response.StatusCode = statusCode;
        ep.HttpContext.Response.ContentType = contentType;

        var xmlSerializer = _serializerCache.GetOrAdd(typeof(TResponse), t => new XmlSerializer(t));

        await using var stream = new MemoryStream();
        xmlSerializer.Serialize(stream, response);
        stream.Position = 0;
        await stream.CopyToAsync(ep.HttpContext.Response.Body, cancellationToken);
    }

    /// <summary>
    /// Sends a FluentResults <see cref="Result"/> response using the project-standard <see cref="BaseResultDTO"/> envelope.
    /// </summary>
    /// <param name="sender">The response sender associated with the current endpoint.</param>
    /// <param name="result">The result to convert and send.</param>
    /// <param name="ct">Token to observe while sending the response.</param>
    public static Task FluentResult(this IResponseSender sender, Result result, CancellationToken ct = default) =>
        SendFluentResultDTOAsync(sender, result.ToResultDTO(), ct: ct);

    /// <summary>
    /// Sends a FluentResults <see cref="Result{T}"/> response using the project-standard <see cref="ResultDTO{T}"/> envelope.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="sender">The response sender associated with the current endpoint.</param>
    /// <param name="result">The result to convert and send.</param>
    /// <param name="ct">Token to observe while sending the response.</param>
    public static Task FluentResult<T>(this IResponseSender sender, Result<T> result, CancellationToken ct = default) =>
        SendFluentResultDTOAsync(sender, result.ToResultDTO(), ct: ct);

    /// <summary>
    /// Sends a FluentResults <see cref="Result{T}"/> response after mapping the value into the response DTO type.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <typeparam name="TResponse">The mapped response DTO type.</typeparam>
    /// <param name="sender">The response sender associated with the current endpoint.</param>
    /// <param name="result">The result to convert and send.</param>
    /// <param name="mapper">Maps a successful result value to the response DTO value.</param>
    /// <param name="ct">Token to observe while sending the response.</param>
    public static async Task FluentResult<T, TResponse>(
        this IResponseSender sender,
        Result<T> result,
        Func<T, TResponse> mapper,
        CancellationToken ct = default
    )
    {
        var resultDTO = result.ToResultDTO(mapper);
        await SendFluentResultDTOAsync(sender, resultDTO, ct: ct);
    }

    private static Task<FastEndpoints.Void> SendFluentResultDTOAsync<TResponse>(
        IResponseSender sender,
        TResponse resultDTO,
        CancellationToken ct = default
    )
        where TResponse : BaseResultDTO =>
        sender.HttpContext.Response.SendAsync(resultDTO, resultDTO.StatusCode, cancellation: ct);
}
