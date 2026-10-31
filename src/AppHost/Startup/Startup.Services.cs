using System.Reflection;
using FastEndpoints.OpenApi;
using FastEndpoints.Security;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.OpenApi;
using Reaparr.Application.Contracts;
using Reaparr.Identity.Contracts;
using Serilog.Sinks.AspNetCore.App.SignalR.Extensions;

namespace Reaparr.AppHost;

/// <summary>
///  The Startup class configures the application services and the HTTP request pipeline.
/// </summary>
public static partial class Startup
{
    /// <summary>
    /// Registers application services, shared HTTP serialization, and the native OpenAPI document-generation pipeline.
    /// </summary>
    /// <param name="services">The service collection to populate before the application service provider is built.</param>
    /// <param name="env">The hosting environment used to select development or production hosting services.</param>
    /// <param name="appRuntimeInfo">
    /// Runtime settings controlling integration-test exclusions and the trusted-proxy authentication header name.
    /// </param>
    public static void ConfigureServices(
        this IServiceCollection services,
        IWebHostEnvironment env,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        // This has to always be first
        services.AddCors(options =>
        {
            options.AddPolicy(
                CorsConfiguration,
                builder =>
                {
                    builder
                        .SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .WithExposedHeaders(ReaparrHeaders.Version, ReaparrHeaders.Platform);
                }
            );
        });

        services.AddOptions();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ApplyStandardConfiguration());

        services.AddMemoryCache();

        services.AddHttpContextAccessor();

        services.ConfigureAuthenticationServices(env, appRuntimeInfo);

        // Set up FastEndpoints
        services.AddFastEndpoints(options =>
        {
            // Manually define the assemblies to scan for FastEndpoints
            options.DisableAutoDiscovery = true;
            options.Assemblies =
            [
                // Reference the assemblies that contain the FastEndpoints or ICommand implementations
                Assembly.GetAssembly(typeof(AppHostModule))!,
                Assembly.GetAssembly(typeof(ApplicationModule))!,
                Assembly.GetAssembly(typeof(DataModule))!,
                Assembly.GetAssembly(typeof(PlexApiModule))!,
                Assembly.GetAssembly(typeof(PublicApiModule))!,
            ];
        });

        // Response caching for downstream caches (browsers, proxies, CDNs) add this after FastEndpoints
        // Doc: https://fast-endpoints.com/docs/response-caching
        services.AddResponseCaching();

        services.AddCommandMiddleware(c => c.Register(typeof(ValidationPipeline<,>)));

        if (!appRuntimeInfo.IsIntegrationTestMode)
        {
            // Used to deploy the front-end Nuxt client
            if (env.IsProduction())
            {
                var path = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                    "wwwroot"
                );
                _log.Here().Debug("Setting up SPA static files for production at {Path}", path);
                services.AddSpaStaticFiles(configuration => configuration.RootPath = path);
            }

            if (env.IsDevelopment())
                services.AddSpaStaticFiles(configuration => configuration.RootPath = "ClientApp");

            // Setup SignalR
            services
                .AddSignalR()
                .AddJsonProtocol(options =>
                    options.PayloadSerializerOptions = DefaultJsonSerializerOptions.ConfigStandard
                )
                .AddMessagePackProtocol(options =>
                {
                    options.SerializerOptions = MessagePackSerializerOptions.Standard.WithSecurity(
                        MessagePackSecurity.UntrustedData
                    );
                });

            services.AddSerilogHub<LogHub>();

            services.OpenApiDocument(o =>
            {
                o.Title = "Reaparr Internal API  (NOT FOR EXTERNAL USE)";
                o.DocumentName = "internal";
                o.Version = "v1";

                // https://fast-endpoints.com/docs/swagger-support#swagger-operation-tags
                o.AutoTagPathSegmentIndex = 2;

                // https://fast-endpoints.com/docs/swagger-support#short-schema-names
                o.ShortSchemaNames = true;
                o.EnableJWTBearerAuth = false;
                o.EndpointFilter = ep => !ep.EndpointType.IsDefined(typeof(HideFromOpenApiAttribute), inherit: true);

                o.ConfigureOpenApi = options =>
                {
                    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
                    options.ShouldInclude = description => description.GroupName == "Internal API";
                    var extraTypes = new OpenApiAddExtraTypes(options.CreateSchemaReferenceId);
                    options.AddSchemaTransformer(extraTypes);
                    options.AddDocumentTransformer(extraTypes);
                    options.AddOperationTransformer<OpenApiGlobalHeaders>();
                    options.AddSchemaTransformer(new OpenApiRequiredPropertiesSchemaTransformer());
                    var enumNames = new OpenApiEnumNamesSchemaTransformer(options.CreateSchemaReferenceId);
                    options.AddSchemaTransformer(enumNames);
                    options.AddDocumentTransformer(enumNames);
                };

                // Add cookie-based authentication
                o.AddAuth(
                    "CookieAuth",
                    new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Cookie,
                        Name = DefaultUserAppCredentials.DefaultCookieName,
                        Description = "Cookie-based authentication for the internal Reaparr API",
                    }
                );

                // Header-based authentication
                o.AddAuth(
                    "HeaderAuth",
                    new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = appRuntimeInfo.HeaderAuthTokenName,
                        Description = "Header-based authentication via trusted proxy",
                    }
                );
            });

            // Separate OpenAPI document for the Public API
            services.OpenApiDocument(o =>
            {
                // https://fast-endpoints.com/docs/swagger-support#swagger-operation-tags
                o.AutoTagPathSegmentIndex = 0;

                // https://fast-endpoints.com/docs/swagger-support#short-schema-names
                o.ShortSchemaNames = true;

                o.ConfigureOpenApi = options =>
                {
                    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
                    options.ShouldInclude = description => description.GroupName == "Public API";
                    var enumNames = new OpenApiEnumNamesSchemaTransformer(options.CreateSchemaReferenceId);
                    options.AddSchemaTransformer(enumNames);
                    options.AddDocumentTransformer(enumNames);
                };

                o.Title = "Public API";
                o.DocumentName = "public";
                o.Version = "v1";
            });
        }

        services.RegisterDefaultHttpClient();

        services.RegisterPlexThumbnailHttpClient();
        services.RegisterGitHubHttpClient(appRuntimeInfo);

        // Removing all registered IHttpMessageHandlerBuilderFilter instances to disable built-in HttpClient logging
        services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
    }

    private static void ConfigureAuthenticationServices(
        this IServiceCollection services,
        IWebHostEnvironment env,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        services.AddDataProtection().PersistKeysToDbContext<AuthDbContext>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AuthenticatedUsers", x => x.RequireRole("Admin"));

            // Set a default policy that requires authentication.
            // Only development may intentionally bypass the fallback policy.
            if (!(env.IsDevelopment() && appRuntimeInfo.IsAuthenticationDisabled))
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        services
            .AddIdentityApiEndpoints<AppUser>(options =>
            {
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 3;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AuthDbContext>();

        services.AddAuthenticationCookie(
            validFor: TimeSpan.FromHours(6),
            c =>
            {
                c.Cookie.Name = DefaultUserAppCredentials.DefaultCookieName;
                c.LoginPath = ApiRoutes.LoginEndpoint;
                c.LogoutPath = ApiRoutes.LogOutEndpoint;
                c.SlidingExpiration = true;
                c.AccessDeniedPath = "/api/access-denied";

                c.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                        ctx.Response.StatusCode = 401;

                    return Task.CompletedTask;
                };
                c.Events.OnRedirectToAccessDenied = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                        ctx.Response.StatusCode = 403;

                    return Task.CompletedTask;
                };
            }
        );
    }
}
