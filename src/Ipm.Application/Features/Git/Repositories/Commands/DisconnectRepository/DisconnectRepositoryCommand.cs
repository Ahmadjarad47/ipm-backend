using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Repositories.Common;

namespace Ipm.Application.Features.Git.Repositories.Commands.DisconnectRepository;

/// <summary>Disconnects a repository, keeping its record and events but wiping its token and secret (R2, git.manage).</summary>
public sealed record DisconnectRepositoryCommand(Guid ProjectId, Guid RepositoryId) : ICommand<RepositoryResponse>;
