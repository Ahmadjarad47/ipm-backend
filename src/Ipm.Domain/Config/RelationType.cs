using System.Text.RegularExpressions;
using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

/// <summary>
/// A kind of link between two work items (blocks, relates, duplicates…) (FR-REL). A directed link reads differently from
/// each side (<see cref="Name"/> / <see cref="InverseName"/>); an undirected one reads the same. Only a directed type can forbid
/// cycles. Once links use the type, its key, direction and cycle rule are frozen (R9, enforced from part 05).
/// </summary>
public sealed partial class RelationType : AuditableEntity
{
    public const int NameMaxLength = 60;

    private RelationType() { }

    private RelationType(Guid id) : base(id) { }

    public string Key { get; private set; } = string.Empty;

    /// <summary>Label from the source side, e.g. "يحجب".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Label from the target side, e.g. "محجوب بواسطة". The same as <see cref="Name"/> for an undirected type.</summary>
    public string InverseName { get; private set; } = string.Empty;

    public bool IsDirected { get; private set; }

    /// <summary>Reject links that would close a cycle (a chain of "blocks").</summary>
    public bool IsAcyclic { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Allowed (source, target) type pairs. Empty means any pair (R8).</summary>
    public List<TypePair> AllowedPairs { get; private set; } = [];

    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    public static bool IsValidKey(string normalizedKey) => KeyPattern().IsMatch(normalizedKey);

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static Result<RelationType> Create(
        string key, string name, string inverseName, bool isDirected, bool isAcyclic, bool isActive, IEnumerable<TypePair> allowedPairs, Guid? id = null)
    {
        var normalized = NormalizeKey(key);
        if (!IsValidKey(normalized))
        {
            return RelationTypeErrors.KeyInvalid;
        }

        var type = id is { } fixedId ? new RelationType(fixedId) : new RelationType();
        type.Key = normalized;
        var result = type.Update(name, inverseName, isDirected, isAcyclic, isActive, allowedPairs);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return type;
    }

    /// <summary>R7: an undirected type has the same name on both sides and cannot forbid cycles.</summary>
    public Result Update(string name, string inverseName, bool isDirected, bool isAcyclic, bool isActive, IEnumerable<TypePair> allowedPairs)
    {
        var trimmedName = name.Trim();
        var trimmedInverse = inverseName.Trim();

        if (!isDirected && trimmedInverse != trimmedName)
        {
            return RelationTypeErrors.InverseNameMustMatch;
        }

        if (!isDirected && isAcyclic)
        {
            return RelationTypeErrors.AcyclicNeedsDirected;
        }

        Name = trimmedName;
        InverseName = trimmedInverse;
        IsDirected = isDirected;
        IsAcyclic = isAcyclic;
        IsActive = isActive;
        AllowedPairs = [.. allowedPairs.Distinct()];
        return Result.Success();
    }

    /// <summary>R6: a request may repeat the key, but never a different one.</summary>
    public Result EnsureKeyUnchanged(string? requestedKey) =>
        requestedKey is null || NormalizeKey(requestedKey) == Key ? Result.Success() : RelationTypeErrors.KeyImmutable;

    [GeneratedRegex("^[a-z][a-z0-9_]{1,30}$")]
    private static partial Regex KeyPattern();
}
