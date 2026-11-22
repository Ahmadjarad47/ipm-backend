using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectMembers;

/// <summary>Replaces the whole member list of the project (R7 R8). The owner must stay a member.</summary>
public sealed record SetProjectMembersCommand(Guid Id, IReadOnlyList<MemberAssignment> Members, uint ExpectedVersion) : ICommand<ProjectMembersResponse>;
