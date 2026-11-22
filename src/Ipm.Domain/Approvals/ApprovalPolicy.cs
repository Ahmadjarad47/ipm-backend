namespace Ipm.Domain.Approvals;

public static class ApprovalPolicy
{
    /// <summary>R9: an approved request can authorize its action for this many days only.</summary>
    public const int TtlDays = 14;

    /// <summary>R8: the most requests one bulk decision may carry.</summary>
    public const int MaxBulk = 50;

    /// <summary>R13: the longest delegation window, in days.</summary>
    public const int MaxDelegationDays = 90;

    /// <summary>R14: the least time between two reminders of one request.</summary>
    public static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(1);
}
