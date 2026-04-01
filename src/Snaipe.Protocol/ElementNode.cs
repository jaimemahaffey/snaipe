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

    /// <summary>
    /// Set when this element is the root of an instantiated template.
    /// Values: "ControlTemplate" | "ContentTemplate" | "ItemTemplate"
    /// </summary>
    public string? TemplateOrigin { get; init; }

    /// <summary>
    /// For ItemTemplate roots: number of realized item containers currently in the visual tree.
    /// Null for all other template kinds.
    /// </summary>
    public int? TemplateInstanceCount { get; init; }
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
    /// <summary>
    /// True when ValueKind is "Object" and the value is non-null.
    /// Inspector renders a drill-down chevron for these rows.
    /// </summary>
    public bool IsObjectValued { get; init; }
    /// <summary>
    /// When set, this property row is a template navigation target.
    /// Values: "ControlTemplate" | "ContentTemplate" | "ItemTemplate"
    /// </summary>
    public string? TemplateOriginKind { get; init; }
    /// <summary>
    /// All currently-active contributing sources for this dependency property, ordered
    /// highest→lowest precedence. Null for synthetic entries (Data Context, Style meta, etc.)
    /// and when the only source is the metadata default.
    /// </summary>
    public List<ValueChainEntry>? ValueChain { get; init; }
}

/// <summary>
/// One entry in a dependency property value chain.
/// </summary>
public sealed class ValueChainEntry
{
    /// <summary>
    /// Source label. One of:
    ///   "Local" | "Binding" | "VisualState (StateName)" |
    ///   "Style" | "BasedOn Style" | "Default Style" | "Default"
    /// </summary>
    public required string Source { get; init; }

    /// <summary>Human-readable formatted value string.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// True on the one entry whose value is the effective value (highest precedence present).
    /// </summary>
    public bool IsWinner { get; init; }
}
