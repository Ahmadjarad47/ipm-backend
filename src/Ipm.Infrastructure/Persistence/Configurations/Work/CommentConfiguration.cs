using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Work;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        var subjects = string.Join(", ", KebabCaseEnumConverter<CommentSubjectType>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("comments", Schemas.Work, t => t.HasCheckConstraint("ck_comments_subject_type", $"subject_type IN ({subjects})"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.SubjectType).HasConversion<KebabCaseEnumConverter<CommentSubjectType>>().HasMaxLength(16);
        builder.Property(c => c.Body).HasMaxLength(Comment.BodyMaxLength);
        builder.Property(c => c.Revisions).HasJsonbList();
        builder.Property(c => c.MentionedUserIds).HasColumnName("mentioned_user_ids");

        // The subject is a project or a work item, so there is no foreign key; the handler checks it exists.
        builder.HasIndex(c => new { c.SubjectType, c.SubjectId, c.CreatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
