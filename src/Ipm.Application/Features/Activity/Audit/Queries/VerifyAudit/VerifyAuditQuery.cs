using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Activity.Audit.Queries.VerifyAudit;

/// <summary>Spec 08 R7 (audit.view): recomputes the hash chain and reports the first event that no longer matches.</summary>
public sealed record VerifyAuditQuery : IQuery<AuditVerification>;
