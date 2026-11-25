using Ipm.Domain.Activity;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Activity;

internal sealed class TelegramMessageConfiguration : IEntityTypeConfiguration<TelegramMessage>
{
    public void Configure(EntityTypeBuilder<TelegramMessage> builder)
    {
        var statuses = string.Join(", ", KebabCaseEnumConverter<TelegramMessageStatus>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("telegram_outbox", Schemas.Activity, t => t.HasCheckConstraint("ck_telegram_outbox_status", $"status IN ({statuses})"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Text).HasMaxLength(TelegramMessage.TextMaxLength);
        builder.Property(m => m.Url).HasMaxLength(TelegramMessage.UrlMaxLength);
        builder.Property(m => m.LastError).HasMaxLength(TelegramMessage.ErrorMaxLength);
        builder.Property(m => m.Status).HasConversion<KebabCaseEnumConverter<TelegramMessageStatus>>().HasMaxLength(16);
        builder.Ignore(m => m.IsPending);

        // One message per notification (idempotent), and the worker looks for due pending rows.
        builder.HasIndex(m => m.NotificationId).IsUnique();
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
        builder.HasIndex(m => m.UserId);
    }
}
