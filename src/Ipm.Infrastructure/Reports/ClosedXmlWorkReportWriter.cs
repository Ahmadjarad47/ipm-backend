using ClosedXML.Excel;
using Ipm.Application.Abstractions.Reports;
using Ipm.Application.Features.Reports.Work.Common;

namespace Ipm.Infrastructure.Reports;

/// <summary>Spec 10 R4: two right-to-left sheets, a summary per project and the items behind it. Text a spreadsheet would run as a formula is kept as text.</summary>
internal sealed class ClosedXmlWorkReportWriter : IWorkReportWriter
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F5C45");

    public byte[] Write(WorkReportData report)
    {
        using var workbook = new XLWorkbook();
        WriteSummary(workbook.Worksheets.Add("الملخص"), report);
        WriteItems(workbook.Worksheets.Add("عناصر العمل"), report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummary(IXLWorksheet sheet, WorkReportData report)
    {
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = "تقرير العمل";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(2, 1).Value = "الفترة";
        sheet.Cell(2, 2).Value = $"{report.From:yyyy-MM-dd} إلى {report.To:yyyy-MM-dd}";
        sheet.Cell(3, 1).Value = "الشخص";
        Text(sheet.Cell(3, 2), report.PersonName ?? "كل المكلَّفين");
        sheet.Cell(4, 1).Value = "تاريخ الإصدار";
        sheet.Cell(4, 2).Value = report.GeneratedOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        sheet.Range(2, 1, 4, 1).Style.Font.Bold = true;

        string[] headers = ["الرمز", "المشروع", "عناصر تأثّرت بالفترة", "جديدة", "أُنجزت", "مفتوحة الآن", "متأخرة الآن"];
        Header(sheet, 6, headers);
        var row = 7;
        foreach (var p in report.Projects)
        {
            Text(sheet.Cell(row, 1), p.Code);
            Text(sheet.Cell(row, 2), p.Name);
            sheet.Cell(row, 3).Value = p.Touched;
            sheet.Cell(row, 4).Value = p.Created;
            sheet.Cell(row, 5).Value = p.Completed;
            sheet.Cell(row, 6).Value = p.OpenNow;
            sheet.Cell(row, 7).Value = p.OverdueNow;
            row++;
        }

        if (report.Projects.Count > 0)
        {
            sheet.Cell(row, 2).Value = "الإجمالي";
            for (var col = 3; col <= 7; col++)
            {
                sheet.Cell(row, col).FormulaA1 = $"SUM({sheet.Cell(7, col).Address}:{sheet.Cell(row - 1, col).Address})";
            }

            sheet.Range(row, 1, row, 7).Style.Font.Bold = true;
        }

        sheet.Columns().AdjustToContents();
    }

    private static void WriteItems(IXLWorksheet sheet, WorkReportData report)
    {
        sheet.RightToLeft = true;
        string[] headers = ["المرجع", "المشروع", "العنوان", "النوع", "الحالة", "الأولوية", "المكلَّفون", "البداية", "الاستحقاق", "أُنشئ", "آخر تحديث", "متأخر", "سبب الإدراج"];
        Header(sheet, 1, headers);
        var row = 2;
        foreach (var i in report.Items)
        {
            Text(sheet.Cell(row, 1), i.Ref);
            Text(sheet.Cell(row, 2), i.ProjectName);
            Text(sheet.Cell(row, 3), i.Title);
            Text(sheet.Cell(row, 4), i.Type);
            Text(sheet.Cell(row, 5), i.Status);
            Text(sheet.Cell(row, 6), i.Priority);
            Text(sheet.Cell(row, 7), i.Assignees);
            Day(sheet.Cell(row, 8), i.StartDate);
            Day(sheet.Cell(row, 9), i.DueDate);
            Day(sheet.Cell(row, 10), i.CreatedOn);
            Day(sheet.Cell(row, 11), i.UpdatedOn);
            sheet.Cell(row, 12).Value = i.Overdue ? "نعم" : "لا";
            Text(sheet.Cell(row, 13), i.Reasons);
            row++;
        }

        sheet.SheetView.FreezeRows(1);
        if (report.Items.Count > 0)
        {
            sheet.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
        }

        sheet.Columns().AdjustToContents();
        sheet.Column(3).Width = Math.Min(sheet.Column(3).Width, 60);
    }

    private static void Header(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = sheet.Cell(row, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = HeaderFill;
        }
    }

    private static void Day(IXLCell cell, DateOnly? day)
    {
        if (day is { } d)
        {
            cell.Value = d.ToDateTime(TimeOnly.MinValue);
            cell.Style.DateFormat.Format = "yyyy-mm-dd";
        }
    }

    /// <summary>Always text; one that starts like a formula gets an apostrophe in front so a spreadsheet does not run it.</summary>
    internal static string Safe(string value) => value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;

    private static void Text(IXLCell cell, string value) => cell.SetValue(Safe(value));
}
