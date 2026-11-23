using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.GitEvents.Common;

namespace Ipm.Application.Features.Git.GitEvents.Commands.UnlinkGitEvent;

/// <summary>Removes a link between a commit or pull request and a work item, whether it was found automatically or made by hand (R11).</summary>
public sealed record UnlinkGitEventCommand(Guid Id, string Ref) : ICommand<GitEventResponse>;
