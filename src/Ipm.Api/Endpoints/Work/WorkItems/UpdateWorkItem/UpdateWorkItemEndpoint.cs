using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.UpdateWorkItem;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.UpdateWorkItem;

internal sealed class UpdateWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("work-items/{id:guid}", async (Guid id, UpdateWorkItemRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateWorkItemCommand(
                            id, r.Title, r.Description ?? string.Empty, r.Priority, r.StartDate, r.DueDate, r.AssigneeIds ?? [], r.Fields,
                            r.StatusId, r.TypeId, r.ParentId, version), ct))
                    .ToETagResult(http.Response, i => i.Version);
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("تعديل عنصر عمل (If-Match)")
            .WithDescription("العنوان والوصف والأولوية والتواريخ والمسؤولون والحقول. تعديل غير المسؤولين بصلاحية tasks.edit، وتغيير المسؤولين بصلاحية tasks.assign. الحالة والنوع والأب لها عمليات خاصة، وإرسالها مختلفة هون بيرجّع 422.");
}
