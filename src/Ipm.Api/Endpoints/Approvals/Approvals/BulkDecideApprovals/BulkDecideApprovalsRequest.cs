namespace Ipm.Api.Endpoints.Approvals.Approvals.BulkDecideApprovals;

public sealed record BulkDecideApprovalsRequest(IReadOnlyList<Guid>? Ids, string? Comment, Dictionary<Guid, int>? Revisions);
