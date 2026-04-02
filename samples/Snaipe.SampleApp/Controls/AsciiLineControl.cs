// samples/Snaipe.SampleApp/Controls/AsciiLineControl.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Controls;

public sealed class AsciiLineControl : ContentControl
{
    public static readonly DependencyProperty LineProperty =
        DependencyProperty.Register(nameof(Line), typeof(AsciiLine), typeof(AsciiLineControl),
            new PropertyMetadata(null, OnLineChanged));

    public static readonly DependencyProperty OutputFontSizeProperty =
        DependencyProperty.Register(nameof(OutputFontSize), typeof(double), typeof(AsciiLineControl),
            new PropertyMetadata(10.0, OnLineChanged));

    public AsciiLine? Line { get => (AsciiLine?)GetValue(LineProperty); set => SetValue(LineProperty, value); }
    public double OutputFontSize { get => (double)GetValue(OutputFontSizeProperty); set => SetValue(OutputFontSizeProperty, value); }

    private readonly TextBlock _textBlock = new() { FontFamily = new FontFamily("Cascadia Code, Consolas, monospace") };

    public AsciiLineControl()
    {
        Content = _textBlock;
    }

    private static void OnLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AsciiLineControl)d).UpdateLine();

    private void UpdateLine()
    {
        _textBlock.Inlines.Clear();
        _textBlock.FontSize = OutputFontSize;

        if (Line is null) return;

        foreach (var span in Line.Spans)
        {
            var run = new Run { Text = span.Text };
            if (span.Color is { } color)
                run.Foreground = new SolidColorBrush(color);
            _textBlock.Inlines.Add(run);
        }
    }
}
