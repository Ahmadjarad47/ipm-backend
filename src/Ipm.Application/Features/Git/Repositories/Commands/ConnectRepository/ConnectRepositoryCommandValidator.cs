using FluentValidation;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Repositories.Commands.ConnectRepository;

internal sealed class ConnectRepositoryCommandValidator : AbstractValidator<ConnectRepositoryCommand>
{
    public ConnectRepositoryCommandValidator()
    {
        RuleFor(c => c.Provider).IsInEnum();
        RuleFor(c => c.Name).Must(n => n is not null && GitRepository.IsValidName(n)).WithMessage("الصيغة: owner/repository");
        RuleFor(c => c.Url).Must(u => RepositoryUrlPolicy.Validate(u) is null).WithMessage(c => RepositoryUrlPolicy.Validate(c.Url) ?? "رابط غير صالح");
        RuleFor(c => c.DefaultBranch).NotEmpty().MaximumLength(200);
        RuleFor(c => c.AccessToken).NotEmpty().MaximumLength(1000);
        RuleFor(c => c.WebhookSecret).MinimumLength(RepositorySecrets.MinSecretLength).WithMessage("16 حرفاً على الأقل").When(c => c.WebhookSecret is not null);
    }
}
