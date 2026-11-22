using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjectById;

internal sealed class GetProjectByIdQueryHandler(
    IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : IQueryHandler<GetProjectByIdQuery, ProjectResponse>
{
    public async ValueTask<Result<ProjectResponse>> Handle(GetProjectByIdQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ReadAsync(db, currentUser, access, securityLog, query.Id, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var stats = await ProjectWorkStats.LoadAsync(db, [query.Id], RiyadhClock.Today(time), cancellationToken); // R13 of part 05
        return guarded.Value.Project.ToResponse().With(stats);
    }
}
