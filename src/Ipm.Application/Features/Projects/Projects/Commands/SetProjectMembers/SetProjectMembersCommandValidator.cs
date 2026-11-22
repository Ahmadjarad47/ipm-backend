using FluentValidation;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectMembers;

internal sealed class SetProjectMembersCommandValidator : AbstractValidator<SetProjectMembersCommand>
{
    public SetProjectMembersCommandValidator()
    {
        RuleFor(c => c.Members).NotNull();
        RuleForEach(c => c.Members).ChildRules(member =>
        {
            member.RuleFor(m => m.UserId).NotEmpty();
            member.RuleFor(m => m.RoleId).NotEmpty();
        });
    }
}
