using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.ProjectTemplates.Commands.CreateProjectTemplate;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.ProjectTemplates.CreateProjectTemplate;

internal sealed class CreateProjectTemplateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("project-templates", async (CreateProjectTemplateRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateProjectTemplateCommand(
                        r.Name, r.Description ?? string.Empty, r.Active ?? true, r.AllowedTypeIds ?? [], r.DefaultTypeId, r.InitialStatusId,
                        r.SuggestedRoleIds ?? [], r.DurationDays, r.SuggestedMembers ?? []), ct))
                    .ToCreatedETagResult(response, t => t.Version, t => $"/api/v1/project-templates/{t.Id}"))
            .RequireAuthorization(Permissions.SettingsManage)
            .WithTags(ProjectsTags.Templates)
            .WithSummary("إنشاء قالب مشروع")
            .WithDescription("الحالة الابتدائية (إن انبعتت) لازم تكون الابتدائية لسير عمل المشاريع. الأنواع موجودة والافتراضي منها، والأدوار والأعضاء المقترحون بنطاق project.");
}
