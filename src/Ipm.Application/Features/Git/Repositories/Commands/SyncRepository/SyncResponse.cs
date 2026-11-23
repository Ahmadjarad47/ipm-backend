using Ipm.Application.Features.Git.Repositories.Common;

namespace Ipm.Application.Features.Git.Repositories.Commands.SyncRepository;

/// <summary>How many commits the server returned, how many were new, and the work item refs they linked.</summary>
public sealed record SyncResponse(int Fetched, int Ingested, IReadOnlyList<string> LinkedRefs, RepositoryResponse Repository);
