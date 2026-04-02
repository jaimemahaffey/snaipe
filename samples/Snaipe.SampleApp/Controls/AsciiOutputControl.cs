// samples/Snaipe.SampleApp/Controls/AsciiOutputControl.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Snaipe.SampleApp.ViewModels;
using Windows.UI;

namespace Snaipe.SampleApp.Controls;

public sealed class AsciiOutputControl : Control
{
    // ── Dependency Properties ─────────────────────────────────────────

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(AsciiDocument), typeof(AsciiOutputControl),
            new PropertyMetadata(null, OnDocumentChanged));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(ConversionStatus), typeof(AsciiOutputControl),
            new PropertyMetadata(ConversionStatus.Idle, OnStatusChanged));

    public static readonly DependencyProperty ProgressPercentProperty =
        DependencyProperty.Register(nameof(ProgressPercent), typeof(int), typeof(AsciiOutputControl),
            new PropertyMetadata(0, OnProgressChanged));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(AsciiOutputControl),
            new PropertyMetadata(string.Empty, OnErrorChanged));

    public static readonly DependencyProperty OutputFontSizeProperty =
        DependencyProperty.Register(nameof(OutputFontSize), typeof(double), typeof(AsciiOutputControl),
            new PropertyMetadata(10.0, OnDocumentChanged));

    public AsciiDocument? Document { get => (AsciiDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public ConversionStatus Status { get => (ConversionStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public int ProgressPercent { get => (int)GetValue(ProgressPercentProperty); set => SetValue(ProgressPercentProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public double OutputFontSize { get => (double)GetValue(OutputFontSizeProperty); set => SetValue(OutputFontSizeProperty, value); }

    // ── Template parts ────────────────────────────────────────────────

    private ItemsControl? _itemsControl;
    private ProgressBar? _progressBar;
    private TextBlock? _errorText;

    public AsciiOutputControl()
    {
        DefaultStyleKey = typeof(AsciiOutputControl);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _itemsControl = GetTemplateChild("PART_ItemsControl") as ItemsControl;
        _progressBar  = GetTemplateChild("PART_Progress")     as ProgressBar;
        _errorText    = GetTemplateChild("PART_ErrorText")    as TextBlock;
        UpdateDisplayState();
        RenderDocument();
    }

    // ── DP change callbacks ───────────────────────────────────────────

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AsciiOutputControl)d).RenderDocument();

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AsciiOutputControl)d).UpdateDisplayState();

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AsciiOutputControl)d;
        if (ctrl._progressBar is not null)
            ctrl._progressBar.Value = (int)e.NewValue;
    }

    private static void OnErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AsciiOutputControl)d;
        if (ctrl._errorText is not null)
            ctrl._errorText.Text = (string)e.NewValue;
    }

    // ── Rendering ────────────────────────────────────────────────────

    private void RenderDocument()
    {
        if (_itemsControl is null) return;

        if (Document is not { Lines.Count: > 0 })
        {
            _itemsControl.ItemsSource = null;
            return;
        }

        _itemsControl.ItemsSource = Document.Lines;
    }

    private void UpdateDisplayState()
    {
        var state = Status switch
        {
            ConversionStatus.Idle when Document is null => "Empty",
            ConversionStatus.Idle                       => "Idle",
            ConversionStatus.Done                       => "Idle",
            ConversionStatus.Converting                 => "Converting",
            ConversionStatus.Error                      => "Error",
            _                                           => "Empty"
        };
        VisualStateManager.GoToState(this, state, true);
    }
}
