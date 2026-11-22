using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.Relations.Common;

/// <summary>A relation as one of its two items sees it: <see cref="Label"/> is the type name when this item is the source, and the inverse name when it is the target.</summary>
public sealed record RelationViewResponse(RelationResponse Relation, string Label, WorkItemResponse Other, bool Outgoing);
