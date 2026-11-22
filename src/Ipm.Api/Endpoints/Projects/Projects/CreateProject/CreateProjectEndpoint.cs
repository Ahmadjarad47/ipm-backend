using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.CreateProject;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.CreateProject;

internal sealed class CreateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects", async (CreateProjectRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateProjectCommand(r.Code, r.Name, r.Description ?? string.Empty, r.OwnerId, r.StartDate, r.EndDate, r.TemplateId), ct))
                    .ToCreatedETagResult(response, p => p.Version, p => $"/api/v1/projects/{p.Id}"))
            .RequireAuthorization(Permissions.ProjectsCreate)
            .WithTags(ProjectsTags.Projects)
            .WithSummary("إنشاء مشروع")
            .WithDescription("بيبدأ بحالة «قيد الإعداد». المالك والمنشئ بينضافوا «مدير مشروع». templateId بيعبّي الأنواع المسموحة والأعضاء المقترحين. الرمز بأحرف كبيرة ولا يتغيّر.");
}
