namespace Ipm.Application.Features.Reports.Work.Common;

/// <summary>Everything the work report shows: the period and filters it was made for, one summary line per project, and the items behind them.</summary>
public sealed record WorkReportData(
    DateOnly From,
    DateOnly To,
    DateOnly GeneratedOn,
    string? PersonName,
    IReadOnlyList<WorkReportProjectSummary> Projects,
    IReadOnlyList<WorkReportRow> Items);
