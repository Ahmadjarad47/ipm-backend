namespace Ipm.Application.Features.Reports.Work.Common;

/// <summary><paramref name="Reasons"/> says why the item is in the period: created, completed, status changed, or updated.</summary>
public sealed record WorkReportRow(
    string Ref,
    string ProjectName,
    string Title,
    string Type,
    string Status,
    string Priority,
    string Assignees,
    DateOnly? StartDate,
    DateOnly? DueDate,
    DateOnly CreatedOn,
    DateOnly UpdatedOn,
    bool Overdue,
    string Reasons);
