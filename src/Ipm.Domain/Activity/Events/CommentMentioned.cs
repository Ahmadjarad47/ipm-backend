using Ipm.Domain.Common;
using Ipm.Domain.Work;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: a comment mentions people (only those newly mentioned by an edit).</summary>
public sealed record CommentMentioned(Guid CommentId, CommentSubjectType SubjectType, Guid SubjectId, IReadOnlyList<Guid> Mentioned, string Preview) : DomainEvent;
