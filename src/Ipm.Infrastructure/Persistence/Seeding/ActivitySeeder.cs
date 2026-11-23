using Ipm.Domain.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 08 §6. Development only (<c>Seed:Demo</c>): five sample notifications for each main demo user, some read and some not, with fixed ids so the seed is
/// idempotent. The audit log is not seeded: it is built from use.
/// </summary>
public static class ActivitySeeder
{
    private static readonly string[] Users = ["reem", "fahad", "khaled", "noura", "abdullah"];

    private static readonly (string Kind, string Title, string Body, string Link, bool Read)[] Samples =
    [
        (NotificationKinds.WorkItemAssigned, "تم تكليفك بعنصر عمل", "ERP-104 تحسين شاشة الدفعات", "/projects", false),
        (NotificationKinds.WorkItemStatusChanged, "تغيّرت حالة عنصر عمل", "ERP-105: قيد التنفيذ ← قيد المراجعة", "/projects", false),
        (NotificationKinds.ApprovalRequested, "أُسند إليك طلب موافقة", "إغلاق عنصر ERP-111", "/approvals", false),
        (NotificationKinds.CommentMentioned, "تمت الإشارة إليك في تعليق", "هل يمكن مراجعة التصميم قبل الاجتماع؟", "/projects", true),
        (NotificationKinds.ProjectMemberAdded, "أُضفت إلى مشروع", "نظام تخطيط الموارد", "/projects", true),
    ];

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Seed:Demo"))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await db.Users.Where(u => Users.Contains(u.UserName!)).Select(u => new { u.Id, u.UserName }).ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        for (var u = 0; u < Users.Length; u++)
        {
            var user = users.Find(x => x.UserName == Users[u]);
            if (user is null)
            {
                continue;
            }

            for (var k = 0; k < Samples.Length; k++)
            {
                var id = new Guid($"01970000-0000-7000-800c-{u * 10 + k:D12}");
                if (await db.Notifications.AnyAsync(n => n.Id == id, cancellationToken))
                {
                    continue;
                }

                var (kind, title, body, link, read) = Samples[k];
                var at = now.AddHours(-(k + 1) * 3);
                db.Notifications.Add(Notification.Create(user.Id, kind, title, body, link, at, id: id, readAt: read ? at.AddMinutes(30) : null));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
