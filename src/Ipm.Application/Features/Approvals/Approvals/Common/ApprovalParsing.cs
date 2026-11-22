using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>Reads the kebab-case texts of query strings (<c>work-item</c>, <c>pending-approval</c>), which the model binder cannot turn into enums.</summary>
internal static class ApprovalParsing
{
    public static bool TryParseSubjectType(string? text, out ApprovalSubjectType type)
    {
        type = text == "project" ? ApprovalSubjectType.Project : ApprovalSubjectType.WorkItem;
        return text is "project" or "work-item";
    }

    public static bool TryParseStatus(string? text, out ApprovalStatus status)
    {
        status = text switch
        {
            "approved" => ApprovalStatus.Approved,
            "rejected" => ApprovalStatus.Rejected,
            "returned" => ApprovalStatus.Returned,
            "superseded" => ApprovalStatus.Superseded,
            _ => ApprovalStatus.PendingApproval,
        };
        return text is "pending-approval" or "approved" or "rejected" or "returned" or "superseded";
    }

    public static bool TryParseAction(string? text, out ApprovalAction action)
    {
        action = text switch
        {
            "reopen" => ApprovalAction.Reopen,
            "change" => ApprovalAction.Change,
            "activate" => ApprovalAction.Activate,
            _ => ApprovalAction.Close,
        };
        return text is "close" or "reopen" or "change" or "activate";
    }
}
