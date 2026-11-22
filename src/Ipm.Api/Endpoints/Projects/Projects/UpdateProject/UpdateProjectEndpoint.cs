using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.UpdateProject;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.UpdateProject;

internal sealed class UpdateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("projects/{id:guid}", async (Guid id, UpdateProjectRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateProjectCommand(id, r.Code, r.StatusId, r.Name, r.Description ?? string.Empty, r.StartDate, r.EndDate, r.OwnerId, version), ct))
                    .ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("تعديل بيانات المشروع (If-Match)")
            .WithDescription("بصلاحية projects.edit داخل المشروع. الاسم والوصف والتواريخ والمالك (لازم يكون عضواً). الرمز والحالة ما بيتغيّروا من هون. المؤرشف للقراءة فقط (422).");
}
