using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.MatchApprovalTemplate;

internal sealed class MatchApprovalTemplateQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<MatchApprovalTemplateQuery, ApprovalTemplateResponse?>
{
    public async ValueTask<Result<ApprovalTemplateResponse?>> Handle(MatchApprovalTemplateQuery query, CancellationToken cancellationToken)
    {
        var subjectType = ApprovalParsing.TryParseSubjectType(query.SubjectType, out var parsedType) ? parsedType : default;
        var action = ApprovalParsing.TryParseAction(query.Action, out var parsedAction) ? parsedAction : default;
        var projectId = await ApprovalSubjects.ProjectIdAsync(db, subjectType, query.SubjectId, cancellationToken);
        if (projectId is null)
        {
            return ApprovalErrors.SubjectNotFound;
        }

        var guarded = await ProjectGuard.ReadAsync(db, currentUser, access, securityLog, projectId.Value, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error == ProjectErrors.NotFound ? ApprovalErrors.SubjectNotFound : guarded.Error;
        }

        var subject = (await ApprovalSubjects.LoadAsync(db, subjectType, query.SubjectId, cancellationToken))!;
        var template = await TemplateMatcher.BestAsync(db, subject, action, cancellationToken);
        return template?.ToResponse();
    }
}
