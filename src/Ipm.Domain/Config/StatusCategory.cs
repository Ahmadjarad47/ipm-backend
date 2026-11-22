namespace Ipm.Domain.Config;

/// <summary>Stable reporting category of a status; its display name may change freely (FR-WF-03). Shown as kebab-case text.</summary>
public enum StatusCategory
{
    NotStarted = 0,
    Active = 1,
    Blocked = 2,
    Done = 3,
    Cancelled = 4,
}
