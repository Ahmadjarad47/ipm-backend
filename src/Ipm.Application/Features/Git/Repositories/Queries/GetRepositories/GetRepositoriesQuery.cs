using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Repositories.Common;

namespace Ipm.Application.Features.Git.Repositories.Queries.GetRepositories;

/// <summary>The repositories of a project, without any secret (git.view).</summary>
public sealed record GetRepositoriesQuery(Guid ProjectId) : IQuery<IReadOnlyList<RepositoryResponse>>;
