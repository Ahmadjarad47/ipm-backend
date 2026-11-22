using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Relations.Commands.CreateRelation;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Relations.CreateRelation;

internal sealed class CreateRelationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("relations", async (CreateRelationRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateRelationCommand(r.SourceId, r.TargetId, r.RelationTypeId, r.SourceVersion, r.TargetVersion), ct))
                    .ToCreatedResult(rel => $"/api/v1/work-items/{rel.SourceId}/relations"))
            .WithTags(WorkTags.Relations)
            .WithSummary("ربط عنصرين")
            .WithDescription("بصلاحية workitem.relations.create بمشروع المصدر. نوع الرابط مفعّل وبيسمح بزوج الأنواع، بدون تكرار (ولا عكسه) وبدون حلقة إذا النوع acyclic. الروابط بين المشاريع مرفوضة حالياً. sourceVersion و targetVersion اختياريان لفحص التزامن (412).");
}
