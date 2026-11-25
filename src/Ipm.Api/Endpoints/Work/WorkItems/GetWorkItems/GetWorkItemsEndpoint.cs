using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItems;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.GetWorkItems;

internal sealed class GetWorkItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-items", async (
                    ISender sender,
                    CancellationToken ct,
                    Guid? projectId = null,
                    string? typeId = null,
                    string? statusId = null,
                    string? priority = null,
                    string? assigneeId = null,
                    bool unassigned = false,
                    Guid? parentId = null,
                    bool? hasChildren = null,
                    bool? hasDependency = null,
                    bool? unblocked = null,
                    Guid? blockedBy = null,
                    Guid? blocking = null,
                    string? q = null,
                    bool overdue = false,
                    bool includeArchived = false,
                    DateOnly? createdFrom = null,
                    DateOnly? createdTo = null,
                    DateOnly? dueFrom = null,
                    DateOnly? dueTo = null,
                    string? sort = null,
                    bool desc = false,
                    int page = 1,
                    int pageSize = 50) =>
                (await sender.Send(
                    new GetWorkItemsQuery(
                        projectId, ListParameter.Guids(typeId, "typeId"), ListParameter.Guids(statusId, "statusId"), ListParameter.Words(priority), ListParameter.Guids(assigneeId, "assigneeId"), unassigned, parentId, hasChildren, hasDependency, unblocked, blockedBy, blocking, q, overdue, includeArchived,
                        createdFrom, createdTo, dueFrom, dueTo, sort, desc, new PageRequest(page, pageSize)), ct))
                    .ToHttpResult())
            .WithTags(WorkTags.WorkItems)
            .WithSummary("الأعمال بالمشاريع التي تراها مع الفلاتر والترقيم")
            .WithDescription("typeId وstatusId وpriority وassigneeId تقبل أكثر من قيمة مفصولة بفواصل (أي واحدة منها). q بالرقم المرجعي أو العنوان. overdue=true للمتأخر (استحقاق قبل اليوم وغير منجز أو ملغى). assigneeId للمكلّف، unassigned للبدون مسؤول. sort: ref title priority dueDate status type createdAt، و desc=true للتنازلي. المؤرشف مخفي إلا مع includeArchived=true. pageSize حتى 200.");
}
