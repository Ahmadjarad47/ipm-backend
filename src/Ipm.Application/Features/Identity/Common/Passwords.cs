using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Ipm.Application.Features.Identity.Common;

/// <summary>
/// Password policy (R5): at least 8 characters, with a letter and a digit.
/// Checked by FluentValidation for an Arabic message, then by Identity validators.
/// </summary>
internal static class Passwords
{
    public const int MinLength = 8;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty()
        .MinimumLength(MinLength).WithMessage($"كلمة المرور يجب أن تكون {MinLength} أحرف على الأقل.")
        .MaximumLength(128)
        .Matches(@"\p{L}").WithMessage("كلمة المرور يجب أن تحتوي على حرف واحد على الأقل.")
        .Matches(@"\d").WithMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل.");

    /// <summary>Hashes a new password after the Identity validators, and rotates the security stamp (old tokens stop working).</summary>
    public static async Task<Result> SetAsync(UserManager<User> users, User user, string password)
    {
        foreach (var validator in users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(users, user, password);
            if (!check.Succeeded)
            {
                return AuthErrors.WeakPassword("كلمة المرور لا تحقق شروط الأمان: 8 أحرف على الأقل مع حرف ورقم.");
            }
        }

        user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        return Result.Success();
    }
}
