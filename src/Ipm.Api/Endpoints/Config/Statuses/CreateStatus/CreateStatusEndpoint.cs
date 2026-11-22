using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Statuses.Commands.CreateStatus;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Statuses.CreateStatus;

internal sealed class CreateStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("statuses", async (CreateStatusRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(
                    new CreateStatusCommand(r.Key, r.Name, r.Description ?? string.Empty, r.Color, r.Category, r.Scope, r.Order ?? 0, r.Active ?? true), ct))
                    .ToCreatedETagResult(response, s => s.Version, s => $"/api/v1/statuses/{s.Id}"))
            .RequireAuthorization(Permissions.StatusesManage)
            .WithTags(ConfigTags.Statuses)
            .WithSummary("إنشاء حالة")
            .WithDescription("المفتاح فريد داخل النطاق وبيتحوّل لأحرف صغيرة. المفتاح والنطاق ثابتين بعد الإنشاء. category: not-started active blocked done cancelled.");
}
