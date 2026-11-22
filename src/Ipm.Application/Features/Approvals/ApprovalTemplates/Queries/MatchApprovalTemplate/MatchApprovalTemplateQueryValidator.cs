using FluentValidation;
using Ipm.Application.Features.Approvals.Approvals.Common;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.MatchApprovalTemplate;

internal sealed class MatchApprovalTemplateQueryValidator : AbstractValidator<MatchApprovalTemplateQuery>
{
    public MatchApprovalTemplateQueryValidator()
    {
        RuleFor(q => q.SubjectType).Must(s => ApprovalParsing.TryParseSubjectType(s, out _)).WithMessage("نوع الموضوع: project أو work-item.");
        RuleFor(q => q.SubjectId).NotEmpty();
        RuleFor(q => q.Action).Must(a => ApprovalParsing.TryParseAction(a, out _)).WithMessage("الإجراء: close أو reopen أو change أو activate.");
    }
}
