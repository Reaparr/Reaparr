using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Reaparr.Logging;

public static partial class LogExtensions
{
    private static readonly ConcurrentDictionary<Type, bool> _destructureCache = new();

    private static string GetDisplayUrl(this HttpRequest request) => $"{request.Scheme}://{request.Host}{request.Path}";

    public static void VerboseApiCall(this ILogger log, HttpContext context, object? request = null)
    {
        if (request is not null)
        {
            if (!ShouldDestructureRequest(request))
            {
                log.Verbose(
                    "{Method}: {EndpointPath} with {RequestType} {Query}",
                    context.Request.Method,
                    context.Request.GetDisplayUrl(),
                    request.GetType().Name,
                    context.Request.Query
                );
                return;
            }

            log.Verbose(
                "{Method}: {EndpointPath} with {@Request} {Query}",
                context.Request.Method,
                context.Request.GetDisplayUrl(),
                request,
                context.Request.Query
            );
            return;
        }

        log.Verbose(
            "{Method}: {EndpointPath} {Query}",
            context.Request.Method,
            context.Request.GetDisplayUrl(),
            context.Request.Query
        );
    }

    public static void DebugApiCall(this ILogger log, HttpContext context, object? request = null)
    {
        if (request is not null)
        {
            if (!ShouldDestructureRequest(request))
            {
                log.Debug(
                    "{Method}: {EndpointPath} with {RequestType}",
                    context.Request.Method,
                    context.Request.GetDisplayUrl(),
                    request.GetType().Name
                );
                return;
            }

            log.Debug(
                "{Method}: {EndpointPath} with {@Request}",
                context.Request.Method,
                context.Request.GetDisplayUrl(),
                request
            );
            return;
        }

        log.Debug(
            "{Method}: {EndpointPath} {Query}",
            context.Request.Method,
            context.Request.GetDisplayUrl(),
            context.Request.Query
        );
    }

    /// <summary>
    /// Why this exists:
    /// Serilog destructuring can aggressively traverse object graphs. For request DTOs that contain
    /// upload abstractions (IFormFile), streams, ASP.NET request/response objects, or dynamic proxies,
    /// this can become extremely expensive and can appear as a hang in tests.
    /// In those cases we log the request type only instead of destructuring the entire payload.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private static bool ShouldDestructureRequest(object request)
    {
        var requestType = request.GetType();
        return _destructureCache.GetOrAdd(requestType, static t =>
        {
            // Moq/Castle proxy types frequently include deep/cyclic members and are unsafe to destructure.
            if (string.Equals(t.Assembly.GetName().Name, "DynamicProxyGenAssembly2", StringComparison.Ordinal))
                return false;

            if (t.Namespace?.StartsWith("Castle.Proxies", StringComparison.Ordinal) == true)
                return false;

            return !IsPotentiallyUnsafeForDestructuring(t, []);
        });
    }

    private static bool IsPotentiallyUnsafeForDestructuring(Type type, HashSet<Type> visitedTypes)
    {
        if (!visitedTypes.Add(type))
            return false;

        if (typeof(IFormFile).IsAssignableFrom(type))
            return true;

        if (typeof(IFormFileCollection).IsAssignableFrom(type))
            return true;

        if (typeof(IFormCollection).IsAssignableFrom(type))
            return true;

        if (typeof(Stream).IsAssignableFrom(type))
            return true;

        if (typeof(HttpContext).IsAssignableFrom(type))
            return true;

        if (typeof(HttpRequest).IsAssignableFrom(type))
            return true;

        if (typeof(HttpResponse).IsAssignableFrom(type))
            return true;

        if (type == typeof(byte[]))
            return true;

        if (type.IsArray)
            return IsPotentiallyUnsafeForDestructuring(type.GetElementType()!, visitedTypes);

        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            foreach (var genericArgument in genericArguments)
            {
                if (IsPotentiallyUnsafeForDestructuring(genericArgument, visitedTypes))
                    return true;
            }
        }

        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;

            if (IsPotentiallyUnsafeForDestructuring(propertyType, visitedTypes))
                return true;
        }

        return false;
    }

    // This adapter intentionally forwards and renders caller-supplied Serilog templates.
    // ReSharper disable TemplateIsNotCompileTimeConstantProblem
    [MessageTemplateFormatMethod("messageTemplate")]
    public static string DebugMsg(this ILogger log, string messageTemplate, params object[] args)
    {
        log.Debug(messageTemplate, args);
        return log.RenderMessage(messageTemplate, args);
    }
}
