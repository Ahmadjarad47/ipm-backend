using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Queries.GetProjectTemplateById;

public sealed record GetProjectTemplateByIdQuery(Guid Id) : IQuery<ProjectTemplateResponse>;
