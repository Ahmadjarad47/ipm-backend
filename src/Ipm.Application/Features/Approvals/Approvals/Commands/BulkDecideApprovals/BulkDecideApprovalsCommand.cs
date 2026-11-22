using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.BulkDecideApprovals;

/// <summary>Approves up to 50 requests at once (R8). <paramref name="Revisions"/> holds, for each request, the revision the caller reviewed.</summary>
public sealed record BulkDecideApprovalsCommand(IReadOnlyList<Guid> Ids, string? Comment, IReadOnlyDictionary<Guid, int> Revisions)
    : ICommand<IReadOnlyList<BulkDecideResult>>;
