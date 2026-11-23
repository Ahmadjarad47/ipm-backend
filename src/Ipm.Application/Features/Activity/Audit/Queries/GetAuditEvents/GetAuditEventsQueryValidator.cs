using FluentValidation;
using Ipm.Application.Features.Activity.Common;

namespace Ipm.Application.Features.Activity.Audit.Queries.GetAuditEvents;

internal sealed class GetAuditEventsQueryValidator : AbstractValidator<GetAuditEventsQuery>
{
    public GetAuditEventsQueryValidator()
    {
        RuleFor(q => q.Source).Must(s => s is null || AuditAccess.Sources.Contains(s)).WithMessage("المصدر: ui أو api أو webhook أو system.");
        RuleFor(q => q.Action).MaximumLength(64);
        RuleFor(q => q.SubjectType).MaximumLength(32);
        RuleFor(q => q.CorrelationId).MaximumLength(64);
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From is not null && q.To is not null).WithMessage("تاريخ النهاية قبل البداية.");
    }
}
