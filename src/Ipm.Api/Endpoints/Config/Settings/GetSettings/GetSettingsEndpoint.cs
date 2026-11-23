using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Settings.Queries.GetSettings;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Settings.GetSettings;

internal sealed class GetSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("settings", async (ISender sender, HttpContext http, CancellationToken ct) =>
                (await sender.Send(new GetSettingsQuery(), ct)).ToETagResult(http.Response, s => s.Version))
            .WithTags(ConfigTags.Settings)
            .WithSummary("إعدادات المؤسسة")
            .WithDescription("لأي مستخدم مسجّل (الواجهة بتحتاج حدود المرفقات والمنطقة الزمنية…). الـ ETag هو الإصدار للتعديل.");
}
