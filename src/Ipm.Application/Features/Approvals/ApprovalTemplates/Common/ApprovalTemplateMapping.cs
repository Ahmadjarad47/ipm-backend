using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Common;

internal static class ApprovalTemplateMapping
{
    public static ApprovalTemplateResponse ToResponse(this ApprovalTemplate template) => new(
        template.Id, template.Name, template.Event, template.IsActive, template.Stages, template.ProtectedFields, template.Rule, template.AllowSelfApproval, template.Version);
}
