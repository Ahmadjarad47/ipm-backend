using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Repositories.Common;

namespace Ipm.Application.Features.Git.Repositories.Commands.SyncRepository;

/// <summary>Reads the latest commits of the default branch from the Git server and links them like a push would (R10, git.sync).</summary>
public sealed record SyncRepositoryCommand(Guid ProjectId, Guid RepositoryId) : ICommand<SyncResponse>;
