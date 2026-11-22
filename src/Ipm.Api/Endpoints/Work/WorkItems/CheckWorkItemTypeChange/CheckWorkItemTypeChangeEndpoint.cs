using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Queries.CheckWorkItemTypeChange;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.CheckWorkItemTypeChange;

internal sealed class CheckWorkItemTypeChangeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items/{id:guid}/type-change/check", async (Guid id, CheckWorkItemTypeChangeRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CheckWorkItemTypeChangeQuery(id, r.TypeId), ct)).ToHttpResult())
            .WithTags(WorkTags.WorkItems)
            .WithSummary("فحص تغيير نوع العنصر بدون تنفيذ")
            .WithDescription("بصلاحية workitem.types.manage داخل المشروع. بيرجّع كل التعارضات: النوع غير مسموح، الحالة غير موجودة بسير العمل الجديد، حقول مطلوبة ناقصة أو ستُفقد، نوع الأب أو الأبناء ما بيسمح. قائمة فاضية = التغيير ممكن.");
}
