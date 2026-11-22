using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ipm.Infrastructure.Persistence.Interceptors;

/// <summary>Stamps created/updated time and user on every <see cref="IAuditable"/> entity before saving.</summary>
public sealed class AuditableEntityInterceptor(IUserContext userContext, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        var userId = userContext.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.SetCreated(now, userId);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.SetUpdated(now, userId);
            }
        }
    }
}
