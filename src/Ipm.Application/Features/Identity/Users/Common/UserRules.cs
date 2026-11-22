using FluentValidation;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Users.Common;

/// <summary>Field rules shared by the user validators.</summary>
internal static class UserRules
{
    public static IRuleBuilderOptions<T, string> Username<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches("^[a-z][a-z0-9._-]{2,30}$").WithMessage("اسم المستخدم: أحرف إنجليزية صغيرة وأرقام و . _ - (3 إلى 31 خانة) ويبدأ بحرف.");

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength);

    public static IRuleBuilderOptions<T, string> PersonTitle<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(User.TitleMaxLength);

    public static IRuleBuilderOptions<T, string> PersonEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(User.EmailMaxLength);
}
