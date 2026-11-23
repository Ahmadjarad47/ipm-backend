using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectRefPattern;

/// <summary>Sets the pattern that finds work item refs in Git texts, or restores the default when empty (part 07 R4, git.manage).</summary>
public sealed record SetProjectRefPatternCommand(Guid Id, string? RefPattern, uint ExpectedVersion) : ICommand<ProjectResponse>;
