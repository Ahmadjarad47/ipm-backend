using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Projects.Projects.Queries.GetProjects;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.GetProjects;

internal sealed class GetProjectsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("projects", async (
                    ISender sender,
                    CancellationToken ct,
                    string? q = null,
                    Guid? statusId = null,
                    Guid? ownerId = null,
                    Guid? memberId = null,
                    bool? archived = null,
                    DateOnly? startFrom = null,
                    DateOnly? startTo = null,
                    DateOnly? endFrom = null,
                    DateOnly? endTo = null,
                    string? sort = null,
                    bool desc = false,
                    int page = 1,
                    int pageSize = 50) =>
                (await sender.Send(
                    new GetProjectsQuery(q, statusId, ownerId, memberId, archived, startFrom, startTo, endFrom, endTo, sort, desc, new PageRequest(page, pageSize)), ct))
                    .ToHttpResult())
            .WithTags(ProjectsTags.Projects)
            .WithSummary("المشاريع التي تراها مع البحث والترقيم")
            .WithDescription("مدير النظام بيشوف الكل، وغيره بيشوف مشاريعه (اللي دوره فيها بيمنح projects.view). q بالرمز أو الاسم. sort: code name startDate endDate، و desc=true للتنازلي. pageSize حتى 200.");
}
