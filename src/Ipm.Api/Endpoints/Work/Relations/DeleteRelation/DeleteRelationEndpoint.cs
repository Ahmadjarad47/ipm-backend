using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Relations.Commands.DeleteRelation;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Relations.DeleteRelation;

internal sealed class DeleteRelationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("relations/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteRelationCommand(id), ct)).ToHttpResult())
            .WithTags(WorkTags.Relations)
            .WithSummary("حذف رابط")
            .WithDescription("بصلاحية workitem.relations.delete بمشروع العنصرين، والمشروع غير مؤرشف. سجل التدقيق بيحفظ أثره.");
}
