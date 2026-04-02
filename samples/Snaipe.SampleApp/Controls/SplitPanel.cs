// samples/Snaipe.SampleApp/Controls/SplitPanel.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Snaipe.SampleApp.Controls;

[Microsoft.UI.Xaml.TemplateVisualState(GroupName = "OrientationStates", Name = "Horizontal")]
[Microsoft.UI.Xaml.TemplateVisualState(GroupName = "OrientationStates", Name = "Vertical")]
public sealed class SplitPanel : Control
{
    // ── Dependency Properties ──────────────────────────────────────────

    public static readonly DependencyProperty Pane1ContentProperty =
        DependencyProperty.Register(nameof(Pane1Content), typeof(object), typeof(SplitPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty Pane2ContentProperty =
        DependencyProperty.Register(nameof(Pane2Content), typeof(object), typeof(SplitPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty Pane1MinSizeProperty =
        DependencyProperty.Register(nameof(Pane1MinSize), typeof(double), typeof(SplitPanel), new PropertyMetadata(80.0));

    public static readonly DependencyProperty Pane2MinSizeProperty =
        DependencyProperty.Register(nameof(Pane2MinSize), typeof(double), typeof(SplitPanel), new PropertyMetadata(80.0));

    public static readonly DependencyProperty SplitterThicknessProperty =
        DependencyProperty.Register(nameof(SplitterThickness), typeof(double), typeof(SplitPanel), new PropertyMetadata(5.0));

    public object? Pane1Content { get => GetValue(Pane1ContentProperty); set => SetValue(Pane1ContentProperty, value); }
    public object? Pane2Content { get => GetValue(Pane2ContentProperty); set => SetValue(Pane2ContentProperty, value); }
    public double Pane1MinSize { get => (double)GetValue(Pane1MinSizeProperty); set => SetValue(Pane1MinSizeProperty, value); }
    public double Pane2MinSize { get => (double)GetValue(Pane2MinSizeProperty); set => SetValue(Pane2MinSizeProperty, value); }
    public double SplitterThickness { get => (double)GetValue(SplitterThicknessProperty); set => SetValue(SplitterThicknessProperty, value); }

    public event EventHandler? SplitterMoved;

    // ── Template parts ────────────────────────────────────────────────

    private Grid? _layoutGrid;
    private Grid? _divider;
    private bool _isDragging;
    private double _dragStartX;
    private double _pane1StartWidth;

    public SplitPanel()
    {
        DefaultStyleKey = typeof(SplitPanel);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_divider is not null)
        {
            _divider.PointerPressed  -= OnDividerPointerPressed;
            _divider.PointerMoved    -= OnDividerPointerMoved;
            _divider.PointerReleased -= OnDividerPointerReleased;
        }

        _layoutGrid = GetTemplateChild("PART_LayoutGrid") as Grid;
        _divider    = GetTemplateChild("PART_Divider")    as Grid;

        if (_divider is not null)
        {
            _divider.PointerPressed  += OnDividerPointerPressed;
            _divider.PointerMoved    += OnDividerPointerMoved;
            _divider.PointerReleased += OnDividerPointerReleased;
        }
    }

    private void OnDividerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_layoutGrid is null) return;
        _isDragging = true;
        _dragStartX = e.GetCurrentPoint(this).Position.X;
        _pane1StartWidth = _layoutGrid.ColumnDefinitions[0].ActualWidth;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnDividerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _layoutGrid is null) return;
        var delta = e.GetCurrentPoint(this).Position.X - _dragStartX;
        var totalWidth = ActualWidth - SplitterThickness;
        var newWidth = Math.Clamp(_pane1StartWidth + delta, Pane1MinSize, totalWidth - Pane2MinSize);
        _layoutGrid.ColumnDefinitions[0].Width = new GridLength(newWidth);
        SplitterMoved?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnDividerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
}
