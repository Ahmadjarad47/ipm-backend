using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Api.Endpoints.Projects.Projects.SetProjectMembers;

public sealed record SetProjectMembersRequest(IReadOnlyList<MemberAssignment>? Members);
