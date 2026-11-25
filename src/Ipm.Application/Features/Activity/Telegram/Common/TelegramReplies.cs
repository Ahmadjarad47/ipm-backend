namespace Ipm.Application.Features.Activity.Telegram.Common;

/// <summary>What the bot answers in the chat (spec 11 R3 and R4). Fixed Arabic text, no user data, so nothing to escape.</summary>
internal static class TelegramReplies
{
    public const string Linked = "تم ربط حسابك ✅\nرح توصلك هون إشعارات التكليف بالمهام. لفك الربط أرسل /stop.";

    public const string Instructions =
        "أهلاً! لربط حسابك افتح ملفك الشخصي في IPM واضغط «اربط تلغرام»، ثم اضغط Start من الرابط اللي بيظهر لك.\nالرابط صالح 10 دقائق ولمرة وحدة.";

    public const string Stopped = "تم فك الربط. ما رح توصلك إشعارات هون بعد الآن.";
}
