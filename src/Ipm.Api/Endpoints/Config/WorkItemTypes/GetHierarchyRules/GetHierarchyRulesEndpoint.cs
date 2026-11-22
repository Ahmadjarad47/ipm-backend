using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Queries.GetHierarchyRules;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.GetHierarchyRules;

internal sealed class GetHierarchyRulesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("hierarchy-rules", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetHierarchyRulesQuery(), ct)).ToHttpResult())
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("قواعد التسلسل الهرمي كأزواج (أب، ابن)")
            .WithDescription("عرض مسطّح لكل الأنواع لرسم المصفوفة. التعديل عبر PUT /work-item-types/{id}/children لكل نوع مع If-Match.");
}
