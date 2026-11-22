namespace Ipm.Application.Features.Approvals.Approvals.Commands.BulkDecideApprovals;

/// <summary>The outcome for one request of a bulk decision (R8).</summary>
public sealed record BulkDecideResult(Guid Id, bool Ok, string? Message);
