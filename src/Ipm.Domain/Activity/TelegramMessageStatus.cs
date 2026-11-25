namespace Ipm.Domain.Activity;

/// <summary>Where a Telegram outbox row is (spec 11 R9). Stored as kebab-case text.</summary>
public enum TelegramMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Expired = 3,
}
