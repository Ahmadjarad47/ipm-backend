using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Projects.Projects.Commands.SetProjectRefPattern;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Repositories.SetProjectRefPattern;

internal sealed class SetProjectRefPatternEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("projects/{id:guid}/ref-pattern", async (Guid id, SetProjectRefPatternRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new SetProjectRefPatternCommand(id, r.RefPattern, version), ct)).ToETagResult(http.Response, p => p.Version);
            })
            .WithTags(GitTags.Repositories)
            .WithSummary("نمط رقم المهمة بالكوميتات (If-Match)")
            .WithDescription("بصلاحية git.manage. regex بمجموعة التقاط واحدة (حتى 100 حرف) بيطلّع رقم المهمة من الرسالة والفرع. null يرجّع الافتراضي. بنختبره على مهام فعلية من المشروع قبل الحفظ.");
}
