using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.ProjectTemplates.Commands.UpdateProjectTemplate;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.ProjectTemplates.UpdateProjectTemplate;

internal sealed class UpdateProjectTemplateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("project-templates/{id:guid}", async (Guid id, UpdateProjectTemplateRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateProjectTemplateCommand(
                            id, r.Name, r.Description ?? string.Empty, r.Active, r.AllowedTypeIds ?? [], r.DefaultTypeId, r.InitialStatusId,
                            r.SuggestedRoleIds ?? [], r.DurationDays, r.SuggestedMembers ?? [], version), ct))
                    .ToETagResult(http.Response, t => t.Version);
            })
            .RequireAuthorization(Permissions.SettingsManage)
            .WithTags(ProjectsTags.Templates)
            .WithSummary("تعديل قالب مشروع وتفعيله أو تعطيله (If-Match)")
            .WithDescription("تعديل القالب ما بيغيّر مشاريع انعملت منه. القالب ما بينحذف، بينعطّل.");
}
