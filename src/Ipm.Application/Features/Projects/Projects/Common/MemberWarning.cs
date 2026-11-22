namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>
/// A non-blocking note after members were changed: a removed member still has open work assigned (the work is left as it is).
/// Empty until work items exist (part 05).
/// </summary>
public sealed record MemberWarning(Guid UserId, string UserName, IReadOnlyList<string> Refs, string Message);
