using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.RelationTypes.Commands.CreateRelationType;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.RelationTypes.CreateRelationType;

internal sealed class CreateRelationTypeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("relation-types", async (CreateRelationTypeRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateRelationTypeCommand(r.Key, r.Name, r.InverseName, r.Directed, r.Acyclic, r.Active ?? true, r.AllowedPairs ?? []), ct))
                    .ToCreatedETagResult(response, t => t.Version, t => $"/api/v1/relation-types/{t.Id}"))
            .RequireAuthorization(Permissions.WorkItemRelationTypesManage)
            .WithTags(ConfigTags.RelationTypes)
            .WithSummary("إنشاء نوع رابط")
            .WithDescription("غير الموجّه اسمه العكسي = اسمه ولا يمنع الحلقات. أزواج الأنواع المسموحة الفاضية = أي نوع.");
}
