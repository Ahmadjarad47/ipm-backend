using FluentValidation;
using Ipm.Application.Features.Work.Comments.Common;

namespace Ipm.Application.Features.Work.Comments.Queries.GetComments;

internal sealed class GetCommentsQueryValidator : AbstractValidator<GetCommentsQuery>
{
    public GetCommentsQueryValidator()
    {
        RuleFor(q => q.SubjectType).Must(t => CommentSubjects.TryParse(t, out _)).WithMessage("نوع الموضوع: project أو work-item.");
        RuleFor(q => q.SubjectId).NotEmpty();
    }
}
