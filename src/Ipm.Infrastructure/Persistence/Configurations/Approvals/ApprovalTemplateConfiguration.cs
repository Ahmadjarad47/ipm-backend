using Ipm.Domain.Approvals;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Approvals;

internal sealed class ApprovalTemplateConfiguration : IEntityTypeConfiguration<ApprovalTemplate>
{
    public void Configure(EntityTypeBuilder<ApprovalTemplate> builder)
    {
        var events = string.Join(", ", KebabCaseEnumConverter<ApprovalEvent>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("approval_templates", Schemas.Approvals, t => t.HasCheckConstraint("ck_approval_templates_event", $"event IN ({events})"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(ApprovalTemplateDefinition.NameMaxLength);
        builder.Property(t => t.Event).HasConversion<KebabCaseEnumConverter<ApprovalEvent>>().HasMaxLength(24);
        builder.Property(t => t.Stages).HasJsonbList();
        builder.Property(t => t.ProtectedFields).HasColumnName("protected_fields");
        builder.Property(t => t.Rule).HasJsonbObject();

        builder.HasIndex(t => new { t.Event, t.IsActive });
    }
}
