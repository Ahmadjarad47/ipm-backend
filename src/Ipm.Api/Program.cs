using System.Text.Json;
using System.Text.Json.Serialization;
using Ipm.Api.Endpoints;
using Ipm.Api.Infrastructure;
using Ipm.Application;
using Ipm.Infrastructure;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Enums travel as kebab-case text, the same as the front end's string unions and the database (on-hold, in-progress).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false)));

builder.Services.AddArabicProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "IPM API";
    document.Info.Description = "روابط نظام إدارة المشاريع IPM. كل جزء من النظام مجموعة مستقلة. "
        + "كل الروابط تحتاج تسجيل دخول (Authorization: Bearer <accessToken>) ما عدا اللي مذكور غير هيك. "
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

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.ApplyMigrationsAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs", options => options
        .WithTitle("IPM API")
        .AddPreferredSecuritySchemes("Bearer"));
}

app.MapHealthChecks("/health");

// Everything under /api/v1 needs a signed-in user unless an endpoint says AllowAnonymous.
var api = app.MapGroup("/api/v1").RequireAuthorization();
app.MapEndpoints(api);

await app.RunAsync();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
