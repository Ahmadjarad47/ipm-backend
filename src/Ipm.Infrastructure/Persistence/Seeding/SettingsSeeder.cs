using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>Spec 09 §6. Always: the single settings record with the default values (the system needs it to run). Idempotent.</summary>
public static class SettingsSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.Settings.AnyAsync(cancellationToken))
        {
            db.Settings.Add(SystemSettings.CreateDefault());
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
