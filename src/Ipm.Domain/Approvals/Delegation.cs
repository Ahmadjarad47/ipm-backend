using Ipm.Domain.Common;

namespace Ipm.Domain.Approvals;

/// <summary>
/// A temporary hand-over of approval authority from one user to another (FR-APR-11). Active inside its window (both dates included, by the
/// Riyadh calendar). Never deleted, only revoked (R13).
/// </summary>
public sealed class Delegation : AuditableEntity
{
    public const int ReasonMaxLength = 500;

    private Delegation() { }

    private Delegation(Guid id) : base(id) { }

    public Guid FromUserId { get; private set; }

    public Guid ToUserId { get; private set; }

    public DateOnly FromDate { get; private set; }

    public DateOnly ToDate { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>R13: the dates order, the length, and not already over. The users are checked by the handler.</summary>
    public static Result<Delegation> Create(Guid fromUserId, Guid toUserId, DateOnly fromDate, DateOnly toDate, string reason, DateOnly today)
    {
        if (toDate < fromDate)
        {
            return Error.Validation("Delegation.InvalidDates", "تاريخ النهاية يسبق البداية.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > ApprovalPolicy.MaxDelegationDays)
        {
            return Error.Validation("Delegation.TooLong", $"الحد الأقصى {ApprovalPolicy.MaxDelegationDays} يوماً.");
        }

        if (toDate < today)
        {
            return Error.Validation("Delegation.AlreadyOver", "الفترة منتهية مسبقاً.");
        }

        if (fromUserId == toUserId)
        {
            return Error.Validation("Delegation.ToSelf", "لا يمكن التفويض لنفسك.");
        }

        return new Delegation { FromUserId = fromUserId, ToUserId = toUserId, FromDate = fromDate, ToDate = toDate, Reason = reason.Trim() };
    }

    /// <summary>True when the delegation is on and <paramref name="today"/> is inside the window.</summary>
    public bool IsInEffect(DateOnly today) => IsActive && FromDate <= today && today <= ToDate;

    public bool Overlaps(DateOnly from, DateOnly to) => FromDate <= to && from <= ToDate;

    public Result Revoke()
    {
        if (!IsActive)
        {
            return ApprovalErrors.DelegationRevoked;
        }

        IsActive = false;
        return Result.Success();
    }
}
