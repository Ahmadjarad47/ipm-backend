using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Queries.GetProjectTemplateById;

internal sealed class GetProjectTemplateByIdQueryHandler(IApplicationDbContext db)
    : IQueryHandler<GetProjectTemplateByIdQuery, ProjectTemplateResponse>
{
    public async ValueTask<Result<ProjectTemplateResponse>> Handle(GetProjectTemplateByIdQuery query, CancellationToken cancellationToken)
    {
        var template = await db.ProjectTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
        return template is null ? ProjectTemplateErrors.NotFound : template.ToResponse();
    }
}
