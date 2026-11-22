using FluentValidation;
using Ipm.Application.Features.Approvals.Approvals.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovals;

internal sealed class GetApprovalsQueryValidator : AbstractValidator<GetApprovalsQuery>
{
    public GetApprovalsQueryValidator()
    {
        RuleFor(q => q.Box).Must(b => b is null or "inbox" or "sent" or "closed").WithMessage("الصندوق: inbox أو sent أو closed.");
        RuleFor(q => q.Status).Must(s => s is null || ApprovalParsing.TryParseStatus(s, out _)).WithMessage("حالة غير معروفة.");
        RuleFor(q => q.SubjectType).Must(s => s is null || ApprovalParsing.TryParseSubjectType(s, out _)).WithMessage("نوع الموضوع: project أو work-item.");
    }
}
