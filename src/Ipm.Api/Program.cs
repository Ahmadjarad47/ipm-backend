using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ipm.Api.Endpoints;
using Ipm.Api.Endpoints.Identity.Auth.Common;
using Ipm.Api.Infrastructure;
using Ipm.Application;
using Ipm.Infrastructure;
using Ipm.RealTime;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddRealTime();

// Enums travel as kebab-case text, the same as the front end string unions and the database (on-hold, in-progress).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false)));

builder.Services.AddArabicProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "IPM API";
    document.Info.Description = "روابط نظام إدارة المشاريع IPM. كل جزء من النظام مجموعة مستقلة. "
        + "سجّل الدخول من POST /api/v1/auth/login ثم أرسل Authorization: Bearer <accessToken> مع كل رابط (ما عدا رابطي الدخول والتجديد). "
        + "قراءة سجل واحد ترجع ETag، والتعديل والحذف يطلبان If-Match بنفس القيمة.";

    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));
builder.Services.AddEndpoints(typeof(Program).Assembly);

// Behind a reverse proxy the client IP and scheme come from X-Forwarded-*. The proxy address inside the Docker network
// is not fixed, so known proxies are cleared; this is safe only because the API port is not published and every
// request reaches it through the proxy.
var behindProxy = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Brute-force protection on sign-in and refresh, per client IP (R1, R2).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(SessionCookie.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
    options.AddPolicy(SessionCookie.RefreshRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:RefreshPerMinute", 120),
            Window = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.ApplyMigrationsAsync();
}

if (app.Configuration.GetValue<bool>("Seed:OnStartup"))
{
    await app.Services.SeedDatabaseAsync(app.Configuration);
}

if (behindProxy)
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
// The API answers are per user and change with every write: a browser or proxy must never keep one.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options => options
        .WithTitle("IPM API")
        .AddPreferredSecuritySchemes("Bearer"));
}

app.MapHealthChecks("/health");

// Real-time hubs (spec 08 R11), under /api/v1 so they ride the existing nginx/dev-proxy rule for that prefix.
app.MapRealTime();

// Everything under /api/v1 needs a signed-in user unless an endpoint says AllowAnonymous.
var api = app.MapGroup("/api/v1").RequireAuthorization();
app.MapEndpoints(api);

await app.RunAsync();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
