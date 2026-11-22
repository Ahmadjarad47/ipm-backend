using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.Comments.Queries.GetCommentRevisions;

/// <summary>The text history of a comment; the first entry is the original. Empty for a deleted comment (R16).</summary>
public sealed record GetCommentRevisionsQuery(Guid Id) : IQuery<IReadOnlyList<CommentRevision>>;
