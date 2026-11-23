using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.GitEvents.Common;

namespace Ipm.Application.Features.Git.GitEvents.Commands.LinkGitEvent;

/// <summary>Links a commit or pull request to a work item of the same project by hand (R11, tasks.edit).</summary>
public sealed record LinkGitEventCommand(Guid Id, string Ref) : ICommand<GitEventResponse>;
