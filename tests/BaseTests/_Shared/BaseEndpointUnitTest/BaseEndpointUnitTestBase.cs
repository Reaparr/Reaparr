using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reaparr.PublicAPI;

namespace Reaparr.BaseTests;

/// <summary>
/// Shared FastEndpoints unit-test infrastructure for constructing endpoints, executing request
/// validation, and capturing endpoint responses in a deterministic in-memory HTTP context.
/// </summary>
/// <remarks>
/// Endpoint tests use this layer instead of calling <c>Factory.Create</c> directly so that every
/// test receives the same Reaparr services, mock hub dependencies, optional database-backed
/// contexts, and response-body capture behavior.
/// </remarks>
/// <typeparam name="TEndpoint">The endpoint type under test.</typeparam>
/// <typeparam name="TResponse">The response DTO type expected by the concrete endpoint helper.</typeparam>
public abstract class BaseEndpointUnitTestBase<TEndpoint, TResponse> : BaseUnitTest<TEndpoint>
    where TEndpoint : class, IEndpoint
    where TResponse : class
{
    /// <summary>
    /// Runs the endpoint request validator, when one exists, before invoking the endpoint handler.
    /// </summary>
    /// <typeparam name="TRequest">The request DTO type being validated.</typeparam>
    /// <param name="request">The request DTO passed to the endpoint test.</param>
    /// <param name="cancellationToken">Cancellation token for validator execution.</param>
    /// <returns>The validation result, or <see langword="null"/> when the request type has no validator.</returns>
    protected static async Task<FluentValidation.Results.ValidationResult?> ValidateEndpointRequestAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken
    )
        where TRequest : class
    {
        var requestType = typeof(TRequest);

        var validatorTypes = requestType
            .Assembly.GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsInterface: false }
                && t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IValidator<>)
                        && i.GenericTypeArguments[0] == requestType
                    )
            )
            .ToList();

        var validator = validatorTypes.Count switch
        {
            0 => null,
            1 => (IValidator)Activator.CreateInstance(validatorTypes[0])!,
            _ => throw new InvalidOperationException(
                $"Multiple validators found for endpoint request type '{requestType.FullName}'. Endpoint unit tests expect at most one validator."
            ),
        };
        if (validator is null)
            return null;

        var context = new FluentValidation.ValidationContext<TRequest>(request);
        return await validator.ValidateAsync(context, cancellationToken);
    }

    /// <summary>
    /// Captures a response from a FastEndpoints endpoint without requiring every endpoint to assign
    /// the <c>Response</c> property directly.
    /// </summary>
    /// <param name="endpoint">The endpoint instance after handler execution.</param>
    /// <param name="cancellationToken">Cancellation token for response-body deserialization.</param>
    /// <returns>The typed response when it can be read, otherwise <see langword="null"/>.</returns>
    /// <remarks>
    /// FastEndpoints can produce responses in multiple ways: assigning <c>Response</c>, writing JSON
    /// through <c>SendAsync</c>, sending project FluentResult DTOs, or writing plain text. This helper
    /// checks those paths in that order and resets the body stream after reading so tests may still
    /// assert against the raw response body when needed.
    /// </remarks>
    protected static async Task<TResponse?> GetEndpointResponseAsync(
        TEndpoint endpoint,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var responseProperty = endpoint.GetType().GetProperty(nameof(Endpoint<,>.Response));
            if (responseProperty?.GetValue(endpoint) is TResponse response)
                return response;
        }
        catch (Exception ex)
            when (ex is NotSupportedException or TargetInvocationException { InnerException: NotSupportedException })
        {
            // Some FastEndpoints response DTOs cannot be auto-created because they have required members.
            // Those endpoints still write their response to the HTTP body via SendAsync/Send.FluentResult.
        }

        if (endpoint.HttpContext.Response.Body is not MemoryStream body || body.Length == 0)
            return null;

        if (typeof(TResponse) == typeof(string))
        {
            body.Position = 0;
            using var reader = new StreamReader(body, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            body.Position = 0;
            return text as TResponse;
        }

        try
        {
            body.Position = 0;
            var response = await JsonSerializer.DeserializeAsync<TResponse>(
                body,
                DefaultJsonSerializerOptions.ConfigStandard,
                cancellationToken
            );
            body.Position = 0;
            return response;
        }
        catch (JsonException)
        {
            body.Position = 0;
            return null;
        }
    }

    protected T SetupEndpointUnitTest<T>(Action<IServiceCollection>? extraServices = null)
        where T : class, IEndpoint
    {
        return Factory.Create<T>(ctx =>
        {
            ctx.AddTestServices(s =>
            {
                // All different dependencies that are needed for the endpoint need to be added here. And then they can be mocked in the test.
                s.AddTransient(_ => Mock.Create<ILogger>());
                s.AddTransient(_ => IsDatabaseSetup ? IDbContext : Mock.Create<IReaparrDbContext>());
                s.AddTransient(_ => Mock.Mock<IReaparrDbContextFactory>().Object);
                s.AddTransient(_ => IsDatabaseSetup ? IAuthDbContext : Mock.Create<IAuthDbContext>());
                s.AddTransient(_ => Mock.Create<IAuthDbContextFactory>());
                s.AddTransient(_ => Mock.Mock<ICommandExecutor>().Object);
                s.AddSingleton(_ => Mock.Create<IScheduler>());
                s.AddSingleton(_ => Mock.Mock<IProgressHubService>().Object);
                s.AddSingleton(_ => Mock.Mock<IDownloadHubService>().Object);
                s.AddSingleton(_ => Mock.Mock<INotificationHubService>().Object);
                s.AddSingleton(_ => Mock.Mock<IDownloadTaskScheduler>().Object);
                s.AddSingleton(_ => Mock.Container.Resolve<IPathProvider>());
                s.AddSingleton(_ => Mock.Container.Resolve<IAppBuildInfo>());
                s.AddSingleton(_ => Mock.Mock<IHostApplicationLifetime>().Object);
                extraServices?.Invoke(s);
            });
        });
    }

    protected async Task AuthenticateIntegrationAsync(TEndpoint endpoint, IntegrationIdentity identity)
    {
        var dbContext = IDbContext;
        var apiKey =
            identity.Type == IntegrationType.Sonarr
                ? await dbContext
                    .SonarrIntegrations.Where(x => x.Id == identity.Id)
                    .Select(x => x.QBittorrentApiKey)
                    .SingleAsync()
                : await dbContext
                    .RadarrIntegrations.Where(x => x.Id == identity.Id)
                    .Select(x => x.QBittorrentApiKey)
                    .SingleAsync();

        endpoint.HttpContext.Request.RouteValues["integrationId"] = identity.Id.ToString();
        endpoint.HttpContext.Request.Headers.Authorization = $"Bearer {apiKey}";
        var authenticatedIdentity = await endpoint.HttpContext.AuthenticateBearerAsync(dbContext, CancellationToken);
        authenticatedIdentity.ShouldBe(identity);
    }

    protected async Task AuthenticateEndpointIntegrationAsync(TEndpoint endpoint, IntegrationIdentity? identity)
    {
        if (identity is null)
            return;

        await AuthenticateIntegrationAsync(endpoint, identity);
    }
}
