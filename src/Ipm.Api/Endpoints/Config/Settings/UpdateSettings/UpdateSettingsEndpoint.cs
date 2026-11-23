using System.Text.Json;
using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Settings.Commands.UpdateSettings;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Settings.UpdateSettings;

internal sealed class UpdateSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("settings", async (JsonElement body, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                if (body.ValueKind != JsonValueKind.Object)
                {
                    return Results.Problem(detail: "الجسم لازم يكون كائناً بالإعدادات المراد تغييرها.", statusCode: StatusCodes.Status400BadRequest, extensions: new Dictionary<string, object?> { ["code"] = "Settings.Invalid" });
                }

                var changes = body.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
                return (await sender.Send(new UpdateSettingsCommand(changes, version), ct)).ToETagResult(http.Response, s => s.Version);
            })
            .WithTags(ConfigTags.Settings)
            .WithSummary("تعديل إعدادات المؤسسة (If-Match)")
            .WithDescription("بصلاحية settings.manage. تعديل جزئي: بس المفاتيح المرسلة. مفتاح غير معروف أو قيمة خارج المدى ← 400 بمسار الحقل. كل تعديل بيتدقّق (settings.updated). sessionHours للجلسات الجديدة، والباقي يسري على الطلبات الجاية. المرفقات والاحتفاظ بالتدقيق وخصوصية المشاريع بتتخزّن فقط.");
}
