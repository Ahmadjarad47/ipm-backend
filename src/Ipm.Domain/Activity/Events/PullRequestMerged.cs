using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: a pull request linked to work items was merged; the assignees of those items are told.</summary>
public sealed record PullRequestMerged(Guid GitEventId, Guid ProjectId, string Title, IReadOnlyList<string> Refs) : DomainEvent;
