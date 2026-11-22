using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Delegations.Commands.CreateDelegation;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Delegations.CreateDelegation;

internal sealed class CreateDelegationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("delegations", async (CreateDelegationRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateDelegationCommand(r.FromUserId, r.ToUserId, r.From, r.To, r.Reason ?? string.Empty), ct))
                    .ToCreatedResult(d => $"/api/v1/delegations/{d.Id}"))
            .WithTags(ApprovalsTags.Delegations)
            .WithSummary("تفويض صلاحية الموافقة مؤقتاً")
            .WithDescription("المفوِّض افتراضياً أنت (غيرك بصلاحية approvals.configure). الفترة بتاريخ yyyy-MM-dd حتى 90 يوماً وما انتهت، السبب مطلوب، والمفوَّض إليه نشط وبيملك approvals.decide بمشروع، وما في تفويض متداخل لنفس الزوج.");
}
