// samples/Snaipe.SampleApp/ViewModels/AsciiDocument.cs
using Windows.UI;

namespace Snaipe.SampleApp.ViewModels;

/// <summary>One colored run of characters on a single row.</summary>
public sealed record AsciiSpan(string Text, Color? Color);

/// <summary>One row of the ASCII output, made of colored spans.</summary>
public sealed record AsciiLine(IReadOnlyList<AsciiSpan> Spans);

/// <summary>The full structured output of a conversion.</summary>
public sealed record AsciiDocument(IReadOnlyList<AsciiLine> Lines)
{
    /// <summary>Plain text rendering — strips all color information.</summary>
    public string ToPlainText() =>
        string.Join("\n", Lines.Select(l => string.Concat(l.Spans.Select(s => s.Text))));
}
