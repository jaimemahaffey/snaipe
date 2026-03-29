namespace Snaipe.Protocol;

/// <summary>
/// Represents a single element in the visual tree.
/// </summary>
public sealed class ElementNode
{
    public required string Id { get; init; }
    public required string TypeName { get; init; }
    public string? Name { get; init; }
    public List<PropertyEntry> Properties { get; init; } = [];
    public List<ElementNode> Children { get; init; } = [];
    public BoundsInfo? Bounds { get; init; }
}

public sealed class BoundsInfo
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public sealed class PropertyEntry
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public string? ValueType { get; init; }
    public string? Value { get; init; }
    /// <summary>
    /// Hint for Inspector editors: "String", "Number", "Boolean", "Color", "Thickness", "Enum", "Object".
    /// </summary>
    public string ValueKind { get; init; } = "Object";
    public bool IsReadOnly { get; init; }
    public string? BindingExpression { get; init; }
    /// <summary>
    /// Populated when ValueKind is "Enum". Contains all valid enum member names so the Inspector
    /// can render a ComboBox instead of a free-text field.
    /// </summary>
    public List<string>? EnumValues { get; init; }
}
