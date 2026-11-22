using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.CreateWorkItem;
using Ipm.Domain.Work;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.CreateWorkItem;

internal sealed class CreateWorkItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items", async (CreateWorkItemRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateWorkItemCommand(
                        r.ProjectId, r.TypeId, r.Title, r.Description ?? string.Empty, r.Priority ?? Priority.Medium, r.StartDate, r.DueDate,
                        r.AssigneeIds ?? [], r.ParentId, r.Fields), ct))
                    .ToCreatedETagResult(response, i => i.Version, i => $"/api/v1/work-items/{i.Id}"))
            .WithTags(WorkTags.WorkItems)
            .WithSummary("إنشاء عنصر عمل")
            .WithDescription("بصلاحية tasks.create داخل المشروع (و tasks.assign إذا فيه مسؤولين). الحالة دايماً الابتدائية لسير العمل، والرقم التالي من 101 (ERP-101). النوع لازم يكون مفعّلاً ومسموحاً بالمشروع، والحقول المخصصة بتتحقق بتعريف النوع.");
}
