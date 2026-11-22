using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Users.Common;

internal static class UserMapping
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.UserName!,
        user.Name,
        user.Email!,
        user.Title,
        user.IsActive,
        user.ManagerId,
        user.RoleIds,
        user.MustChangePassword,
        user.Version);

    /// <summary>R20: the view for people who may not administer users.</summary>
    public static UserResponse ToDirectoryResponse(this User user) => user.ToResponse() with
    {
        Email = string.Empty,
        ManagerId = null,
        SystemRoleIds = [],
        MustChangePassword = false,
    };
}
