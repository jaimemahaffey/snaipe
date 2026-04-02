using System.Text.Json.Serialization;

namespace Snaipe.Protocol;

/// <summary>
/// Base class for all messages exchanged between inspector and agent over the IPC channel.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GetTreeRequest), "GetTreeRequest")]
[JsonDerivedType(typeof(GetPropertiesRequest), "GetPropertiesRequest")]
[JsonDerivedType(typeof(SetPropertyRequest), "SetPropertyRequest")]
[JsonDerivedType(typeof(HighlightElementRequest), "HighlightElementRequest")]
[JsonDerivedType(typeof(TreeResponse), "TreeResponse")]
[JsonDerivedType(typeof(PropertiesResponse), "PropertiesResponse")]
[JsonDerivedType(typeof(AckResponse), "AckResponse")]
[JsonDerivedType(typeof(ErrorResponse), "ErrorResponse")]
[JsonDerivedType(typeof(ElementUnderCursorEvent), "ElementUnderCursorEvent")]
[JsonDerivedType(typeof(PickModeActiveEvent), "PickModeActiveEvent")]
public abstract record InspectorMessage
{
    public required string MessageId { get; init; }
}

// --- Inspector → Agent ---

public sealed record GetTreeRequest : InspectorMessage;

public sealed record GetPropertiesRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    /// <summary>
    /// CLR property names to traverse from the element root before reading properties.
    /// Null or empty = read the element's own properties (existing behaviour).
    /// </summary>
    public string[]? PropertyPath { get; init; }
}

public sealed record SetPropertyRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    /// <summary>
    /// CLR property names to traverse from the element root to reach the parent object.
    /// Null or empty = set a property on the element itself (existing behaviour).
    /// </summary>
    public string[]? PropertyPath { get; init; }
    public required string PropertyName { get; init; }
    public required string NewValue { get; init; }
}

public sealed record HighlightElementRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    public bool Show { get; init; } = true;
}

// --- Agent → Inspector (request/response) ---

public sealed record TreeResponse : InspectorMessage
{
    /// <summary>
    /// Element [0] is always the Window.Content subtree.
    /// Additional elements are open popup subtrees tagged TypeName = "[Popup]".
    /// </summary>
    public required List<ElementNode> Roots { get; init; }
}

public sealed record PropertiesResponse : InspectorMessage
{
    public required string ElementId { get; init; }
    public List<PropertyEntry> Properties { get; init; } = [];
}

public sealed record AckResponse : InspectorMessage
{
    /// <summary>
    /// The final value as applied by the agent (e.g. "10.0" if user typed "10")
    /// </summary>
    public string? NormalizedValue { get; init; }
}

public sealed record ErrorResponse : InspectorMessage
{
    public required int ErrorCode { get; init; }
    public required string Error { get; init; }
    public string? Details { get; init; }
}

// --- Agent → Inspector (push events, not responses to requests) ---

/// <summary>
/// Sent by the agent while Ctrl+Shift is held and the pointer moves over an element.
/// The inspector uses this to select and scroll to the element in the tree.
/// </summary>
public sealed record ElementUnderCursorEvent : InspectorMessage
{
    public required string ElementId { get; init; }
    /// <summary>Short type name for the status bar — avoids an extra GetProperties round-trip.</summary>
    public required string TypeName { get; init; }
}

/// <summary>
/// Sent when pick mode activates (modifier held) or deactivates (modifier released).
/// </summary>
public sealed record PickModeActiveEvent : InspectorMessage
{
    public bool Active { get; init; }
}
