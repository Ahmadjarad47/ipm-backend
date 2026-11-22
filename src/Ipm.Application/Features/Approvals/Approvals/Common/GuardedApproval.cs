using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Approvals;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>A request the caller may see, with everything the use cases need to decide what else they may do with it.</summary>
internal sealed record GuardedApproval(ApprovalRequest Request, ApprovalTemplate? Template, Project Project, UserAccess Caller, ApprovalDeciders Deciders)
{
    public bool CanDecide => Deciders.CanDecide(Request, Caller.UserId, Template);
}
