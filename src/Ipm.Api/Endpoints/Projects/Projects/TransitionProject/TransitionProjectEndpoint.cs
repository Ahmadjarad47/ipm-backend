using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.TransitionProject;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.TransitionProject;

internal sealed class TransitionProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("projects/{id:guid}/transition", async (Guid id, TransitionProjectRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new TransitionProjectCommand(id, r.ToStatusId, r.Comment, version), ct))
                    .ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(ProjectsTags.Projects)
            .WithSummary("نقل المشروع إلى حالة أخرى عبر سير العمل (If-Match)")
            .WithDescription("بصلاحية projects.edit داخل المشروع. الانتقال لازم يكون معرّفاً بسير عمل المشاريع. الانتقال اللي بيتطلب موافقة (التفعيل والإغلاق) بيتنفّذ فقط بصلاحية audit.override لحد ما ينبني محرك الموافقات، وبيتسجّل بالسجل.");
}
