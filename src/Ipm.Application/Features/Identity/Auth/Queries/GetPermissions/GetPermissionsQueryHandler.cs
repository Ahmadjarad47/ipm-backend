using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Auth.Queries.GetPermissions;

internal sealed class GetPermissionsQueryHandler : IQueryHandler<GetPermissionsQuery, IReadOnlyList<PermissionGroup>>
{
    public ValueTask<Result<IReadOnlyList<PermissionGroup>>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(Permissions.Groups));
}
