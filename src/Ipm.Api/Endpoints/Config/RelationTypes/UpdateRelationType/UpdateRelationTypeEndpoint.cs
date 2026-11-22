using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.RelationTypes.Commands.UpdateRelationType;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.RelationTypes.UpdateRelationType;

internal sealed class UpdateRelationTypeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("relation-types/{id:guid}", async (Guid id, UpdateRelationTypeRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateRelationTypeCommand(id, r.Key, r.Name, r.InverseName, r.Directed, r.Acyclic, r.Active, r.AllowedPairs ?? [], version), ct))
                    .ToETagResult(http.Response, t => t.Version);
            })
            .RequireAuthorization(Permissions.WorkItemRelationTypesManage)
            .WithTags(ConfigTags.RelationTypes)
            .WithSummary("تعديل نوع رابط وتفعيله أو تعطيله (If-Match)")
            .WithDescription("المفتاح ثابت. القائمة المرسلة لأزواج الأنواع هي القائمة الكاملة.");
}
