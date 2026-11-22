using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Identity.Users.Queries.GetUserDirectory;

/// <summary>Every user in short form, for pickers (assignee, reviewer, manager).</summary>
public sealed record GetUserDirectoryQuery : IQuery<IReadOnlyList<UserDirectoryItem>>;
