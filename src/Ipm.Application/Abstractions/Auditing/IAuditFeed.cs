using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Abstractions.Auditing;

/// <summary>Spec 08: reads the audit log (and the security events) as readable events, checks its chain, and lists it for export.</summary>
public interface IAuditFeed
{
    Task<PagedList<AuditEventResponse>> SearchAsync(AuditFilter filter, AuditVisibility visibility, PageRequest page, CancellationToken cancellationToken);

    Task<int> CountAsync(AuditFilter filter, AuditVisibility visibility, CancellationToken cancellationToken);

    /// <summary>Every matching event, newest first, read in chunks (for the export).</summary>
    IAsyncEnumerable<AuditEventResponse> StreamAsync(AuditFilter filter, AuditVisibility visibility, CancellationToken cancellationToken);

    Task<AuditVerification> VerifyAsync(CancellationToken cancellationToken);

    /// <summary>R8: the export itself is an event of the log (who, the filters, how many).</summary>
    Task RecordExportAsync(Guid? userId, string filters, int count, CancellationToken cancellationToken);
}
