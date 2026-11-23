using FluentValidation;

namespace Ipm.Application.Features.Dashboard.Stats.Queries.GetDashboardStats;

internal sealed class GetDashboardStatsQueryValidator : AbstractValidator<GetDashboardStatsQuery>
{
    public GetDashboardStatsQueryValidator()
    {
        RuleFor(q => q.StatusCategory)
            .Must(s => string.IsNullOrWhiteSpace(s) || GetDashboardStatsQueryHandler.ParseCategories(s) is not null)
            .WithMessage("فئة الحالة: not-started أو active أو blocked أو done أو cancelled (مفصولة بفواصل).");
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From is not null && q.To is not null).WithMessage("تاريخ النهاية قبل البداية.");
    }
}
