using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Config.Workflows.Queries.GetWorkflowHistory;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Workflows.GetWorkflowHistory;

internal sealed class GetWorkflowHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("workflows/{id:guid}/history", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetWorkflowHistoryQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .RequireAuthorization(Permissions.WorkflowsManage)
            .WithTags(ConfigTags.Workflows)
            .WithSummary("سجل تعديل سير العمل (الأحدث أولاً)")
            .WithDescription("من سجل التدقيق: وقت التعديل والمستخدم والقيم قبل وبعد. pageSize حتى 200.");
}
