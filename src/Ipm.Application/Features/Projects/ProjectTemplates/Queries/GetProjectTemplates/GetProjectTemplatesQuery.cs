using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Queries.GetProjectTemplates;

/// <summary>Every project template. No paging: this is a short list of settings.</summary>
public sealed record GetProjectTemplatesQuery(bool? Active) : IQuery<IReadOnlyList<ProjectTemplateResponse>>;
