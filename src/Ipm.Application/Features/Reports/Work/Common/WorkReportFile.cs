namespace Ipm.Application.Features.Reports.Work.Common;

/// <summary>The finished download: the xlsx bytes and the name to save it under.</summary>
public sealed record WorkReportFile(string FileName, byte[] Content);
