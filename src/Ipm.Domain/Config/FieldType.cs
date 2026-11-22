namespace Ipm.Domain.Config;

/// <summary>The kind of value a custom field holds (FR-TYP-06).</summary>
public enum FieldType
{
    Text = 0,
    Number = 1,
    Date = 2,
    Boolean = 3,
    Select = 4,
    User = 5,
}
