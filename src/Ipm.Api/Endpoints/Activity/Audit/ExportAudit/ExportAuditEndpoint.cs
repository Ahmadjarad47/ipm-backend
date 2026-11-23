using System.Globalization;
using System.Text;
using Ipm.Api.Infrastructure;
using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Features.Activity.Audit.Commands.ExportAudit;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Audit.ExportAudit;

/// <summary>R8: streams the filtered log as CSV (UTF-8 with BOM, spreadsheet formulas neutralised). The export itself is logged by the handler.</summary>
internal sealed class ExportAuditEndpoint : IEndpoint
{
    private static readonly string[] Header =
        ["seq", "at", "actorId", "actorName", "action", "subjectType", "subjectId", "subjectLabel", "projectId", "source", "correlationId", "security", "changes"];

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("audit/export", async (
                    ISender sender,
                    HttpContext http,
                    CancellationToken ct,
                    DateOnly? from = null,
                    DateOnly? to = null,
                    Guid? actorId = null,
                    string? action = null,
                    string? subjectType = null,
                    Guid? subjectId = null,
                    Guid? projectId = null,
                    string? source = null,
                    string? correlationId = null,
                    bool? security = null,
                    string? search = null,
                    string format = "csv") =>
            {
                if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Problem(detail: "الصيغة المدعومة: csv.", statusCode: StatusCodes.Status400BadRequest, extensions: new Dictionary<string, object?> { ["code"] = "Audit.FormatUnsupported" });
                }

                var result = await sender.Send(new ExportAuditCommand(from, to, actorId, action, subjectType, subjectId, projectId, source, correlationId, security, search), ct);
                if (result.IsFailure)
                {
                    return result.Error.ToProblem();
                }

                http.Response.ContentType = "text/csv; charset=utf-8";
                http.Response.Headers.ContentDisposition = $"attachment; filename=\"audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv\"";
                await using var writer = new StreamWriter(http.Response.Body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
                await writer.WriteLineAsync(string.Join(',', Header));
                await foreach (var e in result.Value.Rows.WithCancellation(ct))
                {
                    await writer.WriteLineAsync(Row(e));
                }

                await writer.FlushAsync(ct);
                return Results.Empty;
            })
            .WithTags(ActivityTags.Audit)
            .WithSummary("تصدير سجل التدقيق (CSV)")
            .WithDescription("بصلاحية audit.export وبنفس فلاتر القائمة بلا ترقيم. الحد 50,000 سطر (أكثر ← 422 ضيّق الفلاتر). الملف UTF-8 مع BOM، وخلية تبدأ بـ = أو + أو - أو @ بتتحمى من صيغ Excel. التصدير نفسه بيتسجّل بالتدقيق (audit.exported).");

    private static string Row(AuditEventResponse e) => string.Join(',',
        e.Seq.ToString(CultureInfo.InvariantCulture),
        Cell(e.At.ToString("O", CultureInfo.InvariantCulture)),
        Cell(e.ActorId?.ToString()),
        Cell(e.ActorName),
        Cell(e.Action),
        Cell(e.SubjectType),
        Cell(e.SubjectId.ToString()),
        Cell(e.SubjectLabel),
        Cell(e.ProjectId?.ToString()),
        Cell(e.Source),
        Cell(e.CorrelationId),
        e.Security ? "true" : "false",
        Cell(string.Join("; ", e.Changes.Select(c => $"{c.Field}: {c.From} → {c.To}"))));

    /// <summary>Quotes the value, and puts an apostrophe before one a spreadsheet would run as a formula.</summary>
    internal static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var safe = value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
