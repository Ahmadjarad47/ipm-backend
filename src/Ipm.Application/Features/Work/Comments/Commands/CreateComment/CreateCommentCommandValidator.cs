using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Commands.CreateComment;

internal sealed class CreateCommentCommandValidator : AbstractValidator<CreateCommentCommand>
{
    public CreateCommentCommandValidator()
    {
        RuleFor(c => c.SubjectType).IsInEnum();
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().WithMessage("نص التعليق مطلوب.").MaximumLength(Comment.BodyMaxLength);
    }
}
