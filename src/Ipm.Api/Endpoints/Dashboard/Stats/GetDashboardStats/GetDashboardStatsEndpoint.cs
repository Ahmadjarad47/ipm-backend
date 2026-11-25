using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Dashboard.Stats.Queries.GetDashboardStats;
using Mediator;

namespace Ipm.Api.Endpoints.Dashboard.Stats.GetDashboardStats;

internal sealed class GetDashboardStatsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("dashboard/stats", async (
                    ISender sender,
                    CancellationToken ct,
                    Guid? projectId = null,
                    DateOnly? from = null,
                    DateOnly? to = null,
                    string? statusCategory = null,
                    Guid? assigneeId = null) =>
                (await sender.Send(new GetDashboardStatsQuery(projectId, from, to, statusCategory, assigneeId), ct)).ToHttpResult())
            .WithTags(DashboardTags.Dashboard)
            .WithSummary("مؤشرات اللوحة")
            .WithDescription("المشاريع غير المؤرشفة اللي بتشوفها وفيها dashboard.view. from وto يوم بتوقيت المؤسسة، والعمل بيدخل إذا تقاطع مداه مع الفترة. statusCategory قائمة بفواصل: not-started وactive وblocked وdone وcancelled. مشروع غير مرئي ← أرقام صفرية (بدون كشف).");
}
