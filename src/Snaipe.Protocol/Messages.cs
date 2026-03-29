namespace Snaipe.Protocol;

/// <summary>
/// Messages exchanged between inspector and agent over the IPC channel.
/// </summary>
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

public sealed record ErrorResponse : InspectorMessage
{
    public required string Error { get; init; }
}
