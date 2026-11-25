using ClosedXML.Excel;
using Ipm.Application.Abstractions.Reports;
using Ipm.Application.Features.Reports.Work.Common;

namespace Ipm.Infrastructure.Reports;

/// <summary>
/// Spec 10 R4: two right-to-left sheets, a summary per project and the items behind it. Text a spreadsheet would run as a formula is kept as text.
/// Styled with the Raad palette (forest green + wheat gold); cell positions are part of the contract the tests rely on.
/// </summary>
internal sealed class ClosedXmlWorkReportWriter : IWorkReportWriter
{
    private static readonly XLColor Forest = XLColor.FromHtml("#054239");
    private static readonly XLColor Wheat = XLColor.FromHtml("#B9A779");
    private static readonly XLColor WheatSoft = XLColor.FromHtml("#EDEBE0");
    private static readonly XLColor Surface2 = XLColor.FromHtml("#FAF9F5");
    private static readonly XLColor Border = XLColor.FromHtml("#E4E0D3");
    private static readonly XLColor Ink = XLColor.FromHtml("#1C1F1E");
    private static readonly XLColor Muted = XLColor.FromHtml("#5F6662");
    private static readonly XLColor Subtle = XLColor.FromHtml("#8A8F8B");
    private static readonly XLColor Danger = XLColor.FromHtml("#9B2C3A");
    private static readonly XLColor DangerSoft = XLColor.FromHtml("#F4E3E5");

    public byte[] Write(WorkReportData report)
    {
        using var workbook = new XLWorkbook();
        workbook.Style.Font.FontName = "Segoe UI";
        workbook.Style.Font.FontSize = 11;
        workbook.Style.Font.FontColor = Ink;
        workbook.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        WriteSummary(workbook.Worksheets.Add("الملخص"), report);
        WriteItems(workbook.Worksheets.Add("الأعمال"), report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSummary(IXLWorksheet sheet, WorkReportData report)
    {
        const int lastCol = 7;
        sheet.RightToLeft = true;
        sheet.ShowGridLines = false;
        sheet.TabColor = Forest;
        SetWidths(sheet, [16, 30, 20, 12, 12, 15, 15]);

        // Banner: forest block, white title, a wheat rule under it.
        var banner = sheet.Range(1, 1, 1, lastCol);
        banner.Merge();
        banner.Style.Fill.BackgroundColor = Forest;
        banner.Style.Border.BottomBorder = XLBorderStyleValues.Thick;
        banner.Style.Border.BottomBorderColor = Wheat;
        var title = sheet.Cell(1, 1);
        title.Value = "تقرير العمل";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 20;
        title.Style.Font.FontColor = XLColor.White;
        title.Style.Alignment.Indent = 1;
        sheet.Row(1).Height = 44;

        // Meta block: muted label cell, value spanning the rest of the row.
        (string Label, string Value)[] meta =
        [
            ("الفترة", $"{report.From:yyyy-MM-dd}  ←  {report.To:yyyy-MM-dd}"),
            ("الشخص", Safe(report.PersonName ?? "كل المكلَّفين")),
            ("تاريخ الإصدار", report.GeneratedOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
        ];
        for (var i = 0; i < meta.Length; i++)
        {
            var r = 2 + i;
            var label = sheet.Cell(r, 1);
            label.Value = meta[i].Label;
            label.Style.Font.Bold = true;
            label.Style.Font.FontColor = Muted;
            var line = sheet.Range(r, 1, r, lastCol);
            sheet.Range(r, 2, r, lastCol).Merge();
            sheet.Cell(r, 2).SetValue(meta[i].Value);
            sheet.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            line.Style.Fill.BackgroundColor = Surface2;
            line.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            line.Style.Border.BottomBorderColor = Border;
            sheet.Row(r).Height = 24;
        }

        sheet.Row(5).Height = 14;

        string[] headers = ["الرمز", "المشروع", "أعمال تأثّرت بالفترة", "جديدة", "أُنجزت", "مفتوحة الآن", "متأخرة الآن"];
        Header(sheet, 6, headers);
        var row = 7;
        foreach (var p in report.Projects)
        {
            Text(sheet.Cell(row, 1), p.Code);
            Text(sheet.Cell(row, 2), p.Name);
            sheet.Cell(row, 1).Style.Font.FontColor = Muted;
            sheet.Cell(row, 2).Style.Font.Bold = true;
            Count(sheet.Cell(row, 3), p.Touched);
            Count(sheet.Cell(row, 4), p.Created);
            Count(sheet.Cell(row, 5), p.Completed);
            Count(sheet.Cell(row, 6), p.OpenNow);
            Count(sheet.Cell(row, 7), p.OverdueNow, overdue: true);
            BodyRow(sheet, row, lastCol, zebra: (row - 7) % 2 == 1);
            row++;
        }

        if (report.Projects.Count > 0)
        {
            sheet.Cell(row, 2).Value = "الإجمالي";
            for (var col = 3; col <= lastCol; col++)
            {
                var cell = sheet.Cell(row, col);
                cell.FormulaA1 = $"SUM({sheet.Cell(7, col).Address}:{sheet.Cell(row - 1, col).Address})";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.NumberFormat.Format = "#,##0";
            }

            var total = sheet.Range(row, 1, row, lastCol);
            total.Style.Font.Bold = true;
            total.Style.Font.FontColor = Forest;
            total.Style.Fill.BackgroundColor = WheatSoft;
            total.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            total.Style.Border.TopBorderColor = Forest;
            sheet.Row(row).Height = 28;
        }
        else
        {
            sheet.Range(row, 1, row, lastCol).Merge();
            var note = sheet.Cell(row, 1);
            note.Value = "لا توجد مشاريع ضمن هذا التقرير.";
            note.Style.Font.FontColor = Subtle;
            note.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Row(row).Height = 32;
        }

        Print(sheet);
    }

    private static void WriteItems(IXLWorksheet sheet, WorkReportData report)
    {
        sheet.RightToLeft = true;
        sheet.ShowGridLines = false;
        sheet.TabColor = Wheat;
        string[] headers = ["المرجع", "المشروع", "العنوان", "النوع", "الحالة", "الأولوية", "المكلَّفون", "البداية", "الاستحقاق", "أُنشئ", "آخر تحديث", "متأخر", "سبب الإدراج"];
        SetWidths(sheet, [14, 22, 46, 14, 14, 12, 24, 13, 13, 13, 13, 10, 30]);

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

            BodyRow(sheet, row, headers.Length, zebra: (row - 2) % 2 == 1);
            sheet.Cell(row, 1).Style.Font.FontColor = Forest;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            foreach (var col in new[] { 3, 7, 13 })
            {
                sheet.Cell(row, col).Style.Alignment.WrapText = true;
            }

            foreach (var col in new[] { 4, 5, 6, 8, 9, 10, 11, 12 })
            {
                sheet.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var overdue = sheet.Cell(row, 12);
            if (i.Overdue)
            {
                overdue.Style.Font.Bold = true;
                overdue.Style.Font.FontColor = Danger;
                overdue.Style.Fill.BackgroundColor = DangerSoft;
                sheet.Cell(row, 9).Style.Font.FontColor = Danger;
            }
            else
            {
                overdue.Style.Font.FontColor = Subtle;
            }

            row++;
        }

        sheet.SheetView.FreezeRows(1);
        if (report.Items.Count > 0)
        {
            sheet.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
        }

        Print(sheet);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
    }

    private static void SetWidths(IXLWorksheet sheet, double[] widths)
    {
        for (var c = 0; c < widths.Length; c++)
        {
            sheet.Column(c + 1).Width = widths[c];
        }
    }

    private static void Header(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(row, col + 1).Value = headers[col];
        }

        var range = sheet.Range(row, 1, row, headers.Length);
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = Forest;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorderColor = Wheat;
        sheet.Row(row).Height = 32;
    }

    /// <summary>A body row: hairline under it, a faint tint on every second one.</summary>
    private static void BodyRow(IXLWorksheet sheet, int row, int lastCol, bool zebra)
    {
        var range = sheet.Range(row, 1, row, lastCol);
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorderColor = Border;
        if (zebra)
        {
            // Keep a fill a cell already carries (the overdue tint is applied after this, so it wins).
            range.Style.Fill.BackgroundColor = Surface2;
        }

        sheet.Row(row).Height = 24;
    }

    private static void Count(IXLCell cell, int value, bool overdue = false)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = "#,##0";
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        if (value == 0)
        {
            cell.Style.Font.FontColor = Subtle;
        }
        else if (overdue)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = Danger;
        }
    }

    private static void Print(IXLWorksheet sheet)
    {
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Margins.Left = 0.4;
        sheet.PageSetup.Margins.Right = 0.4;
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
