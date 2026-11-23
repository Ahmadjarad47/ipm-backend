using Ipm.Application.Abstractions.Events;
using Ipm.Domain.Activity.Events;

namespace Ipm.IntegrationTests.Infrastructure;

/// <summary>Spec 08 R10: a reaction that throws, to show the request that raised the event still succeeds. It does nothing unless a test turns it on.</summary>
public sealed class FailingStatusHandler : IDomainEventHandler<WorkItemStatusChanged>
{
    public static bool Fail { get; set; }

    public Task Handle(WorkItemStatusChanged domainEvent, CancellationToken cancellationToken) =>
        Fail ? throw new InvalidOperationException("test failure of a notification handler") : Task.CompletedTask;
}
