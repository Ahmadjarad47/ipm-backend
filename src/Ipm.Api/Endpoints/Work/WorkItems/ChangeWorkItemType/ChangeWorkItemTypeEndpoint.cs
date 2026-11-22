using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.ChangeWorkItemType;
using Ipm.Domain.Work;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.ChangeWorkItemType;

internal sealed class ChangeWorkItemTypeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items/{id:guid}/type-change", async (Guid id, ChangeWorkItemTypeRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                var result = await sender.Send(new ChangeWorkItemTypeCommand(id, r.TypeId, version), ct);
                if (result.IsFailure)
                {
                    return result.Error.ToProblem();
                }

                if (result.Value.Item is not { } item)
                {
                    var error = WorkItemErrors.TypeChangeConflictsError;
                    return Results.Problem(
                        detail: error.Description,
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        extensions: new Dictionary<string, object?> { ["code"] = error.Code, ["conflicts"] = result.Value.Conflicts });
                }

                http.Response.SetETag(item.Version);
                return Results.Ok(item);
            })
            .WithTags(WorkTags.WorkItems)
            .WithSummary("تغيير نوع العنصر (If-Match)")
            .WithDescription("بصلاحية workitem.types.manage داخل المشروع. مع أي تعارض ما بيتغيّر شي ويرجّع 422 بقائمة conflicts (field, reason). استخدم type-change/check قبل التنفيذ.");
}
