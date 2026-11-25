using FluentValidation;

namespace Ipm.Application.Features.Reports.Work.Queries.GetWorkReport;

internal sealed class GetWorkReportQueryValidator : AbstractValidator<GetWorkReportQuery>
{
    public const int MaxDays = 366;

    public GetWorkReportQueryValidator()
    {
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).WithMessage("تاريخ النهاية قبل البداية.");
        RuleFor(q => q.To).Must((q, to) => to.DayNumber - q.From.DayNumber < MaxDays).WithMessage($"الفترة أطول من {MaxDays} يوماً. قسّمها إلى فترات أقصر.");
        RuleFor(q => q.ProjectIds).Must(ids => ids.Count <= 100).WithMessage("عدد المشاريع كبير. اختر حتى 100 مشروع أو اتركها فارغة لكل مشاريعك.");
    }
}
