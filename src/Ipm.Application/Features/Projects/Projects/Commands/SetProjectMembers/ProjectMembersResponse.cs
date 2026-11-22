using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectMembers;

/// <summary>The project after the change, plus non-blocking warnings.</summary>
public sealed record ProjectMembersResponse(ProjectResponse Project, IReadOnlyList<MemberWarning> Warnings);
