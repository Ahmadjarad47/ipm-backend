using Ipm.Domain.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Activity;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", Schemas.Activity);
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Kind).HasMaxLength(48);
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(n => n.Body).HasMaxLength(Notification.BodyMaxLength);
        builder.Property(n => n.Link).HasMaxLength(200);
        builder.Property(n => n.SubjectType).HasMaxLength(32);
        builder.Ignore(n => n.IsRead);
        builder.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });
    }
}
