namespace Ipm.Application.Features.Reports.Work.Common;

/// <summary>One project in the summary sheet: how many of its items were touched in the period and where they stand now.</summary>
public sealed record WorkReportProjectSummary(
    string Code,
    string Name,
    int Touched,
    int Created,
    int Completed,
    int OpenNow,
    int OverdueNow);
