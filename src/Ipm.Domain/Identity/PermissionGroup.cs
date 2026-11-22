namespace Ipm.Domain.Identity;

/// <summary>One group of the permission catalog, e.g. <c>projects</c> with its Arabic label.</summary>
public sealed record PermissionGroup(string Group, string Label, IReadOnlyList<PermissionInfo> Permissions);
