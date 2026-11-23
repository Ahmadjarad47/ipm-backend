using Ipm.Application.Abstractions.Auditing;

namespace Ipm.Application.Features.Activity.Common;

/// <summary>R8: the rows to write out, already counted and logged. The API streams them as CSV.</summary>
public sealed record AuditExport(int Count, IAsyncEnumerable<AuditEventResponse> Rows);
