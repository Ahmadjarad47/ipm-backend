using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Statuses.Queries.GetStatuses;
using Ipm.Domain.Config;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Statuses.GetStatuses;

internal sealed class GetStatusesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("statuses", async (ISender sender, CancellationToken ct, string? scope = null, bool? active = null) =>
            {
                var parsed = KebabCaseEnum.ParseQuery<StatusScope>(scope, "scope", out var problem);
                return problem ?? (await sender.Send(new GetStatusesQuery(parsed, active), ct)).ToHttpResult();
            })
            .WithTags(ConfigTags.Statuses)
            .WithSummary("الحالات بترتيب order")
            .WithDescription("scope: project أو work-item. active=true للمفعّلة فقط. بدون ترقيم (قائمة إعدادات قصيرة).");
}
