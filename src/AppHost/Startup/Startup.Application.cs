using System.Reflection;
using Microsoft.AspNetCore.Http.Extensions;

namespace Reaparr.AppHost;

public static partial class Startup
{
    /// <summary>
    /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    /// </summary>
    /// <param name="app">The built web application whose middleware pipeline and endpoint mappings are configured.</param>
    /// <param name="env">The hosting environment used to enable production SPA hosting.</param>
    /// <param name="appBuildInfo">Build metadata used to emit the actual informational-version and runtime-platform response headers.</param>
    /// <param name="appRuntimeInfo">Runtime settings controlling integration-test exclusions and authentication bypass behavior.</param>
    public static void ConfigureApplication(
        this WebApplication app,
        IWebHostEnvironment env,
        IAppBuildInfo appBuildInfo,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        _log.Here()
            .Information(
                "Running location: {Location}",
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            );

        // This has to always be first
        app.UseCors(CorsConfiguration);

        app.Use(
            async (ctx, next) =>
            {
                // Rewrite legacy/public API v2 routes
                if (ctx.Request.Path.StartsWithSegments("/api/v2", out var remaining))
                    ctx.Request.Path = PublicApiRoutes.DownloadClient + remaining;

                // Set global response headers
                ctx.Response.OnStarting(() =>
                {
                    // NOTE: Update "OpenApiGlobalHeaders" when adding/updating headers and add to "CORS WithExposedHeaders" in ConfigureServices
                    ctx.Response.Headers[ReaparrHeaders.Version] = appBuildInfo.InformationalVersion;
                    ctx.Response.Headers[ReaparrHeaders.Platform] = appBuildInfo.RuntimeMode;

                    return Task.CompletedTask;
                });

                await next();
            }
        );

        app.UseRouting();

        if (!appRuntimeInfo.IsIntegrationTestMode)
        {
            // SignalR configuration
            app.MapHub<LogHub>("/logs");
            app.MapHub<ProgressHub>("/progress");
            app.MapHub<DownloadHub>("/download");
            app.MapHub<NotificationHub>("/notifications");

            app.MapOpenApi().AllowAnonymous();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/internal.json", "Internal API");
                options.SwaggerEndpoint("/openapi/public.json", "Public API");
            });
        }

        // Setup production SPA
        if (!appRuntimeInfo.IsIntegrationTestMode && env.IsProduction())
        {
            // Used to deploy the front-end Nuxt client
            app.UseSpaStaticFiles();
            app.UseSpa(spa => spa.Options.SourcePath = "ClientApp");
        }

        // Use custom header authentication middleware
        app.UseMiddleware<HeaderAuthenticationMiddleware>();

        app.UseAuthentication();

        // When I_AM_DUMB_SO_DISABLE_AUTHENTICATION is set, inject a synthetic Admin principal.
        // This must run after UseAuthentication() (so auth middleware does not overwrite it)
        // and before UseAuthorization() (so authorization checks use the synthetic principal).
        if (appRuntimeInfo.IsAuthenticationDisabled)
            app.UseMiddleware<DisableAuthenticationMiddleware>();

        app.UseAuthorization();

        // Enable response caching for downstream caches (must be before FastEndpoints)
        // Doc: https://fast-endpoints.com/docs/response-caching
        app.UseResponseCaching();

        // Set up FastEndpoints
        app.UseFastEndpoints(c =>
        {
            c.Endpoints.Configurator = ep =>
            {
                if (ep.Routes.All(x => x.StartsWith(PublicApiRoutes.Base)))
                    ep.Options(b => b.IsPublicApi());
                else if (ep.Routes.All(x => x.StartsWith(ApiRoutes.Base)))
                    ep.Options(b => b.IsInternalApi());
            };

            // https://fast-endpoints.com/docs/swagger-support#short-endpoint-names
            c.Endpoints.ShortNames = true;

            c.Errors.ResponseBuilder = (failures, ctx, _) =>
            {
                var result = ResultExtensions.Create400BadRequestResult($"Bad request: {ctx.Request.GetDisplayUrl()}");
                var errors = failures
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(e => e.Key, e => string.Join("; ", e.Select(m => m.ErrorMessage)));
                foreach (var reason in errors)
                    result.Errors[0].Metadata.Add(reason.Key, reason.Value);

                return result.LogError();
            };
        });
    }
}
