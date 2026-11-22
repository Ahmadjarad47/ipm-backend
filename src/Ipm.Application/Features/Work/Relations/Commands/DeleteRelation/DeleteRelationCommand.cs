using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Work.Relations.Commands.DeleteRelation;

/// <summary>Removes a link (R7). The audit log keeps the record of it.</summary>
public sealed record DeleteRelationCommand(Guid Id) : ICommand;
