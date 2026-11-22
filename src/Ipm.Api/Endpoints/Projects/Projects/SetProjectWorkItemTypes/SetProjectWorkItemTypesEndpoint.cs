using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkItemTypes;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.SetProjectWorkItemTypes;

internal sealed class SetProjectWorkItemTypesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("projects/{id:guid}/work-item-types", async (Guid id, SetProjectWorkItemTypesRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetProjectWorkItemTypesCommand(id, r.AllowedTypeIds ?? [], r.DefaultTypeId, version), ct))
                    .ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("الأنواع المسموحة بالمشروع والنوع الافتراضي (If-Match)")
            .WithDescription("بصلاحية project.workitemtypes.configure داخل المشروع. نوع واحد على الأقل، والافتراضي منها ومفعّل.");
}
