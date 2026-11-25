using Ipm.Application.Features.Reports.Work.Common;

namespace Ipm.Application.Abstractions.Reports;

/// <summary>Turns a work report into the spreadsheet the user downloads (xlsx). Infrastructure supplies it, so handlers do not know the file library.</summary>
public interface IWorkReportWriter
{
    byte[] Write(WorkReportData report);
}
