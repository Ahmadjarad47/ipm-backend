using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovalById;

/// <summary>A request the caller took part in or may view. Any other answers as if it did not exist (404).</summary>
public sealed record GetApprovalByIdQuery(Guid Id) : IQuery<ApprovalResponse>;
