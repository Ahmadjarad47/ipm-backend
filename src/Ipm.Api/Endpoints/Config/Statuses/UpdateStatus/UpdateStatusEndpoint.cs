using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Statuses.Commands.UpdateStatus;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Statuses.UpdateStatus;

internal sealed class UpdateStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("statuses/{id:guid}", async (Guid id, UpdateStatusRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(
                        new UpdateStatusCommand(id, r.Key, r.Scope, r.Name, r.Description ?? string.Empty, r.Color, r.Category, r.Order, r.Active, version), ct))
                    .ToETagResult(http.Response, s => s.Version);
            })
            .RequireAuthorization(Permissions.StatusesManage)
            .WithTags(ConfigTags.Statuses)
            .WithSummary("تعديل حالة وتفعيلها أو تعطيلها (If-Match)")
            .WithDescription("الحالات ما بتنحذف، بتتعطّل. ممنوع تعطيل الحالة الابتدائية لسير عمل (422).");
}
