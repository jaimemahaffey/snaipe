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
public abstract record InspectorMessage
{
    public required string MessageId { get; init; }
}

// --- Inspector → Agent ---

public sealed record GetTreeRequest : InspectorMessage;

public sealed record GetPropertiesRequest : InspectorMessage
{
    public required string ElementId { get; init; }
}

public sealed record SetPropertyRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    public required string PropertyName { get; init; }
    public required string NewValue { get; init; }
}

public sealed record HighlightElementRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    public bool Show { get; init; } = true;
}

// --- Agent → Inspector ---

public sealed record TreeResponse : InspectorMessage
{
    public required ElementNode Root { get; init; }
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
