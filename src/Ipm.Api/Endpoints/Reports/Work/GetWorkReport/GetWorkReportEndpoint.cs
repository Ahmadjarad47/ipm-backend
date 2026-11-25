using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Reports.Work.Queries.GetWorkReport;
using Mediator;

namespace Ipm.Api.Endpoints.Reports.Work.GetWorkReport;

internal sealed class GetWorkReportEndpoint : IEndpoint
{
    private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("reports/work", async (
                    ISender sender,
                    CancellationToken ct,
                    DateOnly from,
                    DateOnly to,
                    string? projectIds = null,
                    Guid? assigneeId = null) =>
            {
                var ids = new List<Guid>();
                foreach (var part in (projectIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Guid.TryParse(part, out var id))
                    {
                        return Results.Problem(detail: $"معرّف المشروع «{part}» غير صالح.", statusCode: StatusCodes.Status400BadRequest, extensions: new Dictionary<string, object?> { ["code"] = "Report.ProjectIdInvalid" });
                    }

                    ids.Add(id);
                }

                var result = await sender.Send(new GetWorkReportQuery(from, to, ids, assigneeId), ct);
                return result.IsFailure ? result.Error.ToProblem() : Results.File(result.Value.Content, ContentType, result.Value.FileName);
            })
            .WithTags(ReportsTags.Reports)
            .WithSummary("تقرير العمل (xlsx)")
            .WithDescription("ملف Excel بورقتين: ملخص لكل مشروع والأعمال التي أُنشئت أو تغيّرت حالتها أو تحدّثت خلال الفترة. from وto يوم بتوقيت المؤسسة (الفترة حتى 366 يوماً). projectIds قائمة بفواصل (فارغة: كل مشاريعك المسموحة)، وassigneeId يحصر التقرير بأعمال شخص واحد. بصلاحيتي projects.view وreports.export على كل مشروع؛ مشروع غير مسموح لا يظهر ولا يُكشف.");
}
