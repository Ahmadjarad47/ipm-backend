using FluentValidation;
using Ipm.Application.Features.Activity.Common;

namespace Ipm.Application.Features.Activity.Audit.Commands.ExportAudit;

internal sealed class ExportAuditCommandValidator : AbstractValidator<ExportAuditCommand>
{
    public ExportAuditCommandValidator()
    {
        RuleFor(c => c.Source).Must(s => s is null || AuditAccess.Sources.Contains(s)).WithMessage("المصدر: ui أو api أو webhook أو system.");
        RuleFor(c => c.Action).MaximumLength(64);
        RuleFor(c => c.SubjectType).MaximumLength(32);
        RuleFor(c => c.CorrelationId).MaximumLength(64);
        RuleFor(c => c.Search).MaximumLength(100);
        RuleFor(c => c.To).GreaterThanOrEqualTo(c => c.From).When(c => c.From is not null && c.To is not null).WithMessage("تاريخ النهاية قبل البداية.");
    }
}
