using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Delegations.Commands.RevokeDelegation;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Delegations.RevokeDelegation;

internal sealed class RevokeDelegationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("delegations/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RevokeDelegationCommand(id), ct)).ToHttpResult())
            .WithTags(ApprovalsTags.Delegations)
            .WithSummary("إلغاء تفويض")
            .WithDescription("للمفوِّض أو من معه approvals.configure. إلغاء ملغى ← 422.");
}
