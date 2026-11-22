using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Commands.UpdateComment;

internal sealed class UpdateCommentCommandValidator : AbstractValidator<UpdateCommentCommand>
{
    public UpdateCommentCommandValidator() =>
        RuleFor(c => c.Body).NotEmpty().WithMessage("نص التعليق مطلوب.").MaximumLength(Comment.BodyMaxLength);
}
