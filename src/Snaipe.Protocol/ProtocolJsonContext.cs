using System.Text.Json.Serialization;

namespace Snaipe.Protocol;

/// <summary>
/// Source-generated JSON serializer context for AOT compatibility and reduced reflection overhead.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InspectorMessage))]
[JsonSerializable(typeof(GetTreeRequest))]
[JsonSerializable(typeof(GetPropertiesRequest))]
[JsonSerializable(typeof(SetPropertyRequest))]
[JsonSerializable(typeof(HighlightElementRequest))]
[JsonSerializable(typeof(TreeResponse))]
[JsonSerializable(typeof(PropertiesResponse))]
[JsonSerializable(typeof(AckResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ElementUnderCursorEvent))]
[JsonSerializable(typeof(PickModeActiveEvent))]
[JsonSerializable(typeof(ElementNode))]
[JsonSerializable(typeof(BoundsInfo))]
[JsonSerializable(typeof(PropertyEntry))]
[JsonSerializable(typeof(List<ElementNode>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
internal partial class ProtocolJsonContext : JsonSerializerContext;
