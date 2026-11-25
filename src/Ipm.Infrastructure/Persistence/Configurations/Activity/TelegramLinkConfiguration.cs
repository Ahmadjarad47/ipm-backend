using Ipm.Domain.Activity;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Activity;

internal sealed class TelegramLinkConfiguration : IEntityTypeConfiguration<TelegramLink>
{
    public void Configure(EntityTypeBuilder<TelegramLink> builder)
    {
        builder.ToTable("telegram_links", Schemas.Activity);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.TelegramUsername).HasMaxLength(TelegramLink.UsernameMaxLength);
        builder.Ignore(l => l.IsActive);

        // Spec 11 §2: one link per user; the chat index serves "which user is this chat" on Start.
        builder.HasIndex(l => l.UserId).IsUnique();
        builder.HasIndex(l => l.ChatId);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
