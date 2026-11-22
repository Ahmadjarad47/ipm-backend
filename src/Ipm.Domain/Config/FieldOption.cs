namespace Ipm.Domain.Config;

/// <summary>One choice of a <see cref="FieldType.Select"/> field: a stable <paramref name="Value"/> and its Arabic <paramref name="Label"/>.</summary>
public sealed record FieldOption(string Value, string Label);
