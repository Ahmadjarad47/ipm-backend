namespace Ipm.Application.Features.Search.Results.Common;

/// <summary>Spec 09 R15. <see cref="Kind"/> is <c>project</c>, <c>work-item</c>, <c>approval</c> or <c>user</c>; <see cref="Link"/> is a relative path of the front end built from the id.</summary>
public sealed record SearchHit(string Kind, Guid Id, string? Code, string Title, string Subtitle, string Link, Guid? TypeId = null);
