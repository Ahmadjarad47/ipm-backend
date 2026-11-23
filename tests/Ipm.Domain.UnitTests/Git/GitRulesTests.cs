using Ipm.Domain.Git;

namespace Ipm.Domain.UnitTests.Git;

/// <summary>R1, R4 and R7 of docs/modules/07-git-integration.md at the domain level: the ref pattern and the repository URL policy.</summary>
public sealed class GitRulesTests
{
    [Theory]
    [InlineData("ERP-104: fix", "ERP-104")]
    [InlineData("[ERP-104] fix", "ERP-104")]
    [InlineData("feature/ERP-104-login", "ERP-104")]
    [InlineData("see (ERP-104)", "ERP-104")]
    public void The_default_pattern_finds_a_ref_in_the_common_shapes(string text, string expected)
    {
        var found = GitRefPatterns.Extract(GitRefPatterns.Default("ERP"), "ERP", text);

        Assert.Equal([expected], found);
    }

    [Theory]
    [InlineData("XERP-104")]
    [InlineData("ERP104")]
    [InlineData("SSP-104")]
    public void The_default_pattern_ignores_lookalikes_and_other_projects(string text) =>
        Assert.Empty(GitRefPatterns.Extract(GitRefPatterns.Default("ERP"), "ERP", text));

    [Fact]
    public void Refs_are_deduplicated_across_texts_and_only_the_first_2000_characters_are_scanned()
    {
        var found = GitRefPatterns.Extract(GitRefPatterns.Default("ERP"), "ERP", "ERP-1 and ERP-1 and ERP-2", "feature/ERP-2");
        Assert.Equal(["ERP-1", "ERP-2"], found);

        var late = new string('x', 2000) + " ERP-9";
        Assert.Empty(GitRefPatterns.Extract(GitRefPatterns.Default("ERP"), "ERP", late));
    }

    [Theory]
    [InlineData(@"(ERP-\d+)", null)]
    [InlineData(@"\[(ERP-\d+)\]", null)]
    [InlineData(@"ERP-\d+", "مجموعة")]
    [InlineData(@"(\w+)-(\d+)", "مجموعة")]
    [InlineData(@"(?<=x)(ERP-\d+)", "تطلّع")]
    [InlineData(@"(a)\1", "مراجع")]
    [InlineData(@"(NOPE-\d+)", "لا يلتقط")]
    [InlineData("", "فارغ")]
    public void A_pattern_must_have_one_group_no_backreferences_or_lookarounds_and_catch_real_refs(string pattern, string? reasonPart)
    {
        var reason = GitRefPatterns.Validate(pattern, ["ERP-101"]);

        if (reasonPart is null)
        {
            Assert.Null(reason);
        }
        else
        {
            Assert.NotNull(reason);
        }
    }

    [Fact]
    public void A_pattern_over_100_characters_is_refused() =>
        Assert.NotNull(GitRefPatterns.Validate("(" + new string('a', 100) + ")", ["ERP-101"]));

    [Fact]
    public void A_hostile_pattern_runs_in_linear_time_instead_of_hanging()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        GitRefPatterns.Extract(@"(a+)+$", "ERP", new string('a', 1900) + "!");

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void A_stored_pattern_that_no_longer_compiles_falls_back_to_the_default_instead_of_stopping_ingestion() =>
        Assert.Equal(["ERP-5"], GitRefPatterns.Extract("(unclosed", "ERP", "ERP-5 done"));

    [Theory]
    [InlineData("https://git.example.com/acme/app")]
    [InlineData("https://github.com/acme/app")]
    public void A_public_https_url_is_allowed(string url) => Assert.Null(RepositoryUrlPolicy.Validate(url));

    [Theory]
    [InlineData("http://git.example.com/a/b")]
    [InlineData("https://localhost/a/b")]
    [InlineData("https://127.0.0.1/a/b")]
    [InlineData("https://10.1.2.3/a/b")]
    [InlineData("https://192.168.1.1/a/b")]
    [InlineData("https://172.16.0.1/a/b")]
    [InlineData("https://169.254.169.254/a/b")]
    [InlineData("https://[::1]/a/b")]
    [InlineData("https://[::ffff:10.0.0.1]/a/b")]
    [InlineData("https://user:pw@git.example.com/a/b")]
    [InlineData("https://git.example.com:8443/a/b")]
    [InlineData("ftp://git.example.com/a/b")]
    [InlineData("not a url")]
    [InlineData("")]
    public void An_internal_insecure_or_odd_url_is_refused(string url) => Assert.NotNull(RepositoryUrlPolicy.Validate(url));

    [Theory]
    [InlineData("acme/app", true)]
    [InlineData("a/b", true)]
    [InlineData("no-slash", false)]
    [InlineData("a/b/c", false)]
    [InlineData("a b/c", false)]
    [InlineData("", false)]
    public void A_repository_name_is_owner_slash_repository(string name, bool valid) => Assert.Equal(valid, GitRepository.IsValidName(name));
}
