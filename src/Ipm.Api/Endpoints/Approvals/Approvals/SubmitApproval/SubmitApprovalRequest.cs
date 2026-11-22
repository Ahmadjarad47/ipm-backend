using Ipm.Domain.Approvals;

namespace Ipm.Api.Endpoints.Approvals.Approvals.SubmitApproval;

public sealed record SubmitApprovalRequest(Guid TemplateId, ApprovalSubjectType SubjectType, Guid SubjectId, ApprovalAction? Action, Guid? TargetStatusId);
