using System.Security.Cryptography;
using Ipm.Application.Abstractions.Security;
using Ipm.Domain.Git;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 07 §7. Development only (<c>Seed:Demo</c>): the three repositories and four events of the front end. Their tokens and webhook secrets are random
/// and encrypted, never shown. Idempotent: a repository or event that exists is left alone.
/// </summary>
public static class GitSeeder
{
    private static Guid RepoId(int n) => new($"01970000-0000-7000-800a-{n:D12}");

    private static Guid EventId(int n) => new($"01970000-0000-7000-800b-{n:D12}");

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Seed:Demo"))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var projects = await db.Set<Project>().ToDictionaryAsync(p => p.Code, p => p.Id, cancellationToken);

        var repositories = new (int N, string Project, string Name)[] { (1, "ERP", "erp/web-client"), (2, "ERP", "erp/api"), (3, "SSP", "ssp/portal") };
        foreach (var (n, code, name) in repositories)
        {
            if (!projects.TryGetValue(code, out var projectId) || await db.GitRepositories.AnyAsync(r => r.Id == RepoId(n), cancellationToken))
            {
                continue;
            }

            db.GitRepositories.Add(GitRepository.Connect(
                projectId, GitProvider.Gitea, name, $"https://git.example.com/{name}", "main",
                protector.Protect(Random()), protector.Protect(Random()), RepoId(n)));
        }

        await db.SaveChangesAsync(cancellationToken);

        var events = new (int N, string Project, int Repo, string Kind, string Title, string Branch, string Author, string Ref, int? Pr)[]
        {
            (1, "ERP", 1, "push", "ERP-104: تحسين شاشة الدفعات", "feature/ERP-104-payments-screen", "ahmad", "ERP-104", null),
            (2, "ERP", 2, "open", "ERP-105: واجهة ربط المصرف", "feature/ERP-105-bank-api", "omar", "ERP-105", 41),
            (3, "ERP", 2, "merged", "ERP-111: إصلاح تقرير الذمم", "fix/ERP-111-receivables", "sara", "ERP-111", 38),
            (4, "SSP", 3, "push", "SSP-103: صفحة الدخول", "feature/SSP-103-login", "huda", "SSP-103", null),
        };

        foreach (var (n, code, repo, kind, title, branch, author, reference, pr) in events)
        {
            if (!projects.TryGetValue(code, out var projectId) || !await db.GitRepositories.AnyAsync(r => r.Id == RepoId(repo), cancellationToken)
                || await db.GitEvents.AnyAsync(e => e.Id == EventId(n), cancellationToken))
            {
                continue;
            }

            var sha = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"demo-{n}")))[..40];
            var url = $"https://git.example.com/{repositories[repo - 1].Name}/commit/{sha}";
            db.GitEvents.Add(pr is { } number
                ? GitEvent.PullRequest(projectId, RepoId(repo), number, kind == "merged" ? PullRequestState.Merged : PullRequestState.Open, title, branch, sha, author, url, [reference], $"seed-{n}", id: EventId(n))
                : GitEvent.Push(projectId, RepoId(repo), title, branch, sha, author, url, [reference], $"seed-{n}", id: EventId(n)));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Random() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
}
