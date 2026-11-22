using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.GetApprovalTemplateById;

public sealed record GetApprovalTemplateByIdQuery(Guid Id) : IQuery<ApprovalTemplateResponse>;
