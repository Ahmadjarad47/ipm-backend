using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovals;

/// <summary>
/// The approval requests of the caller (R12). <paramref name="Box"/> is <c>inbox</c> (what they can decide now), <c>sent</c> (what they
/// submitted) or <c>closed</c> (not pending, and they took part or may view the project). Newest change first.
/// </summary>
public sealed record GetApprovalsQuery(
    string? Box,
    bool Overdue,
    string? Status,
    Guid? ProjectId,
    string? SubjectType,
    Guid? SubjectId,
    Guid? TemplateId,
    PageRequest Page) : IQuery<PagedList<ApprovalResponse>>;
