using Ipm.Domain.Common;

namespace Ipm.Domain.Activity;

public static class TelegramErrors
{
    /// <summary>R1: the feature is off or has no bot token.</summary>
    public static readonly Error Disabled = Error.Failure("Telegram.Disabled", "ربط تلغرام غير مفعّل على هذا الخادم.");

    public static readonly Error SecretInvalid = Error.Unauthorized("Telegram.SecretInvalid", "الرمز السري للـ webhook غير صحيح.");
}
