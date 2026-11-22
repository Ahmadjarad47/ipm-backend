using System.Reflection;
using System.Text.RegularExpressions;
using Ipm.Api.Endpoints;
using Ipm.Application.Abstractions.Messaging;
using FluentValidation;

namespace Ipm.ArchitectureTests;

/// <summary>
/// Guards the folder convention of every part (README, "Adding a use case"):
/// <code>
/// Application/Features/&lt;Module&gt;/&lt;Resource&gt;/Commands/&lt;Operation&gt;/&lt;Operation&gt;Command.cs (+ Handler, Validator)
/// Application/Features/&lt;Module&gt;/&lt;Resource&gt;/Queries/&lt;Operation&gt;/&lt;Operation&gt;Query.cs   (+ Handler)
/// Api/Endpoints/&lt;Module&gt;/&lt;Resource&gt;/&lt;Operation&gt;/&lt;Operation&gt;Endpoint.cs              (+ Request)
/// </code>
/// and one top-level type per file.
/// </summary>
public sealed partial class FeatureStructureTests
{
    private static readonly Assembly Application = typeof(Application.AssemblyReference).Assembly;
    private static readonly Assembly Api = typeof(IEndpoint).Assembly;

    private static IEnumerable<Type> Messages(Type marker) => Application.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false })
        .Where(t => t.GetInterfaces().Any(i => i == marker || (i.IsGenericType && i.GetGenericTypeDefinition() == marker)));

    public static TheoryData<string, string> MessageKinds => new()
    {
        { "Command", "Commands" },
        { "Query", "Queries" },
    };

    [Theory]
    [MemberData(nameof(MessageKinds))]
    public void Commands_and_queries_live_in_their_own_operation_folder(string suffix, string folder)
    {
        var marker = suffix == "Command" ? typeof(IBaseCommand) : typeof(IQuery<>);
        var violations = Messages(marker)
            .Where(t => !t.Name.EndsWith(suffix, StringComparison.Ordinal)
                || t.Namespace != $"{OperationParent(t)}.{folder}.{t.Name[..^suffix.Length]}")
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Expected Features.<Module>.<Resource>.{folder}.<Operation>.<Operation>{suffix}: {string.Join(", ", violations)}");
    }

    [Theory]
    [MemberData(nameof(MessageKinds))]
    public void Every_command_and_query_has_its_handler_next_to_it(string suffix, string folder)
    {
        _ = folder;
        var marker = suffix == "Command" ? typeof(IBaseCommand) : typeof(IQuery<>);
        var missing = Messages(marker)
            .Where(t => Application.GetType($"{t.FullName}Handler") is null)
            .Select(t => t.FullName)
            .ToList();

        Assert.True(missing.Count == 0, $"Missing <Name>Handler in the same folder: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Validators_sit_next_to_what_they_validate()
    {
        var violations = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.BaseType is { IsGenericType: true } b
                && b.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .Where(t =>
            {
                var target = t.BaseType!.GetGenericArguments()[0];
                return t.Namespace != target.Namespace || t.Name != $"{target.Name}Validator";
            })
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0, $"Validator must be <Name>Validator in the same folder: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Endpoints_are_one_per_operation_folder()
    {
        var violations = Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .Where(t => !t.Name.EndsWith("Endpoint", StringComparison.Ordinal)
                || t.Namespace?.StartsWith("Ipm.Api.Endpoints.", StringComparison.Ordinal) != true
                || t.Namespace.Split('.').Length != 6 // Ipm.Api.Endpoints.<Module>.<Resource>.<Operation>
                || !t.Namespace.EndsWith($".{t.Name[..^"Endpoint".Length]}", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Expected Endpoints.<Module>.<Resource>.<Operation>.<Operation>Endpoint: {string.Join(", ", violations)}");
    }

    [Theory]
    [InlineData("src/Ipm.Application/Features")]
    [InlineData("src/Ipm.Api/Endpoints")]
    public void Each_file_declares_one_top_level_type(string folder)
    {
        var root = Path.Combine(RepositoryRoot(), folder);
        var crowded = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(file => (file, count: TopLevelType().Count(File.ReadAllText(file))))
            .Where(x => x.count > 1)
            .Select(x => $"{Path.GetRelativePath(root, x.file)} ({x.count})")
            .ToList();

        Assert.True(crowded.Count == 0, $"One type per file: {string.Join(", ", crowded)}");
    }

    /// <summary>Ipm.Application.Features.&lt;Module&gt;.&lt;Resource&gt; for a message type.</summary>
    private static string OperationParent(Type t) => string.Join('.', (t.Namespace ?? string.Empty).Split('.').Take(5));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ipm.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Ipm.slnx not found above the test output folder.");
    }

    /// <summary>A type declared at file level (not indented), e.g. <c>public sealed record X</c>.</summary>
    [GeneratedRegex(@"^(public|internal)\s+(?:(?:sealed|static|abstract|partial|readonly)\s+)*(?:class|record|struct|interface|enum)\s", RegexOptions.Multiline)]
    private static partial Regex TopLevelType();
}
