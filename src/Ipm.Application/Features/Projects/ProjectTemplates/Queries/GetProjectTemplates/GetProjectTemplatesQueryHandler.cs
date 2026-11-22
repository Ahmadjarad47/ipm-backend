using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Queries.GetProjectTemplates;

internal sealed class GetProjectTemplatesQueryHandler(IApplicationDbContext db)
    : IQueryHandler<GetProjectTemplatesQuery, IReadOnlyList<ProjectTemplateResponse>>
{
    public async ValueTask<Result<IReadOnlyList<ProjectTemplateResponse>>> Handle(GetProjectTemplatesQuery query, CancellationToken cancellationToken)
    {
        var templates = db.ProjectTemplates.AsNoTracking();
        if (query.Active is { } active)
        {
            templates = templates.Where(t => t.IsActive == active);
        }

        var rows = await templates.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        IReadOnlyList<ProjectTemplateResponse> items = [.. rows.Select(t => t.ToResponse())];
        return Result.Success(items);
    }
}
