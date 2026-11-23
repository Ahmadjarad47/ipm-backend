using Ipm.Domain.Approvals;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Approvals;

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        var statuses = string.Join(", ", KebabCaseEnumConverter<ApprovalStatus>.AllTexts.Select(t => $"'{t}'"));
        var subjects = string.Join(", ", KebabCaseEnumConverter<ApprovalSubjectType>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("approval_requests", Schemas.Approvals, t =>
        {
            t.HasCheckConstraint("ck_approval_requests_status", $"status IN ({statuses})");
            t.HasCheckConstraint("ck_approval_requests_subject_type", $"subject_type IN ({subjects})");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(ApprovalRequest.TitleMaxLength);
        builder.Property(r => r.SubjectType).HasConversion<KebabCaseEnumConverter<ApprovalSubjectType>>().HasMaxLength(16);
        builder.Property(r => r.Status).HasConversion<KebabCaseEnumConverter<ApprovalStatus>>().HasMaxLength(24);
        builder.Property(r => r.Action).HasConversion<KebabCaseEnumConverter<ApprovalAction>>().HasMaxLength(16);
        builder.Property(r => r.Snapshot).HasJsonbList();
        builder.Property(r => r.Decisions).HasJsonbList();
        builder.Property(r => r.Stages).HasJsonbList();
        builder.Property(r => r.ResolvedApprovers).HasJsonbList();
        builder.Property(r => r.ProposedChanges).HasJsonbList();

        builder.HasIndex(r => new { r.SubjectType, r.SubjectId, r.Status });
        builder.HasIndex(r => new { r.ProjectId, r.Status });
        builder.HasIndex(r => r.RequesterId);

        builder.HasOne<ApprovalTemplate>().WithMany().HasForeignKey(r => r.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Status>().WithMany().HasForeignKey(r => r.TargetStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}
