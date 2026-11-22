namespace Ipm.Domain.Config;

/// <summary>
/// An administrator-defined extra field of a work item type. Values live on the work item (part 05), so adding a field
/// never changes a table. <paramref name="Options"/> only applies to <see cref="FieldType.Select"/>;
/// <paramref name="Min"/> and <paramref name="Max"/> to <see cref="FieldType.Number"/>.
/// </summary>
public sealed record FieldDefinition(
    string Key,
    string Label,
    FieldType Type,
    bool Required,
    IReadOnlyList<FieldOption>? Options,
    decimal? Min,
    decimal? Max,
    string? Hint,
    bool ShowInList,
    bool ShowOnCard,
    int Order);
