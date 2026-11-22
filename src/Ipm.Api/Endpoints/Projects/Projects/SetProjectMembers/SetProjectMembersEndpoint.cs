using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.SetProjectMembers;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.SetProjectMembers;

internal sealed class SetProjectMembersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("projects/{id:guid}/members", async (Guid id, SetProjectMembersRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetProjectMembersCommand(id, r.Members ?? [], version), ct))
                    .ToETagResult(http.Response, m => m.Project.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("تعديل أعضاء المشروع وأدوارهم (If-Match)")
            .WithDescription("بصلاحية projects.members.manage داخل المشروع. القائمة المرسلة هي القائمة الكاملة، والمالك فيها دايماً. الأدوار من نطاق project، وما بتمنح ولا بتشيل دوراً فيه صلاحية ما بتملكها بالمشروع. الرد: المشروع مع warnings.");
}
