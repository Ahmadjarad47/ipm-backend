using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.Comments.Common;

namespace Ipm.Application.Features.Work.Comments.Queries.GetComments;

/// <summary>The comments of a project or a work item, oldest first. <paramref name="SubjectType"/> is <c>project</c> or <c>work-item</c> (R16).</summary>
public sealed record GetCommentsQuery(string SubjectType, Guid SubjectId, PageRequest Page) : IQuery<PagedList<CommentResponse>>;
