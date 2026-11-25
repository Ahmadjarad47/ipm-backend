using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.ArchiveWorkItem;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.ArchiveWorkItem;

internal sealed class ArchiveWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("work-items/{id:guid}", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new ArchiveWorkItemCommand(id, version), ct)).ToHttpResult();
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("أرشفة عمل (If-Match)")
            .WithDescription("بصلاحية tasks.delete داخل المشروع. الأعمال ما بتنحذف، بتتأرشف. العمل اللي له أبناء نشطين ما بينأرشف، انقلهم أولاً.");
}
