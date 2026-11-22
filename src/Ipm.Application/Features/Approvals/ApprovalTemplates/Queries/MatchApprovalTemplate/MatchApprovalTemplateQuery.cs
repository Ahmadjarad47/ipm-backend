using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.MatchApprovalTemplate;

/// <summary>The active template that governs <paramref name="Action"/> on the subject, or null when none does (R3).</summary>
public sealed record MatchApprovalTemplateQuery(string SubjectType, Guid SubjectId, string Action) : IQuery<ApprovalTemplateResponse?>;
