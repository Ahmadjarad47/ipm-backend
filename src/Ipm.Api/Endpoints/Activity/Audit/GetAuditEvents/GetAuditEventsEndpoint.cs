using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Activity.Audit.Queries.GetAuditEvents;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Audit.GetAuditEvents;

internal sealed class GetAuditEventsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("audit", async (
                    ISender sender,
                    CancellationToken ct,
                    DateOnly? from = null,
                    DateOnly? to = null,
                    Guid? actorId = null,
                    string? action = null,
                    string? subjectType = null,
                    Guid? subjectId = null,
                    Guid? projectId = null,
                    string? source = null,
                    string? correlationId = null,
                    bool? security = null,
                    string? search = null,
                    int page = 1,
                    int pageSize = 50) =>
                (await sender.Send(new GetAuditEventsQuery(from, to, actorId, action, subjectType, subjectId, projectId, source, correlationId, security, search, new PageRequest(page, pageSize)), ct))
                .ToHttpResult())
            .WithTags(ActivityTags.Audit)
            .WithSummary("سجل التدقيق")
            .WithDescription("من يملك audit.view بيشوف كل شي بما فيه أحداث الأمان؛ غيره أحداث المشاريع اللي بيشوفها فقط. from وto يوم بتوقيت الرياض (الاثنان مشمولان). source: ui أو api أو webhook أو system. security=true لأحداث الأمان فقط. search بالاسم المعروض. الأحدث أولاً وpageSize حتى 200.");
}
