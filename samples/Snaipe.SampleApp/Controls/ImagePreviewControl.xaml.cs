// samples/Snaipe.SampleApp/Controls/ImagePreviewControl.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Snaipe.SampleApp.Controls;

public sealed partial class ImagePreviewControl : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ImagePreviewControl),
            new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(ImagePreviewControl),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double ZoomLevel { get => (double)GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }

    public ImagePreviewControl()
    {
        InitializeComponent();
        ImageScroll.ViewChanged += OnScrollViewChanged;
        Loaded += (_, _) => UpdateVisualState();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ImagePreviewControl)d;
        ctrl.PreviewImage.Source = e.NewValue as ImageSource;
        ctrl.UpdateVisualState();
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ImagePreviewControl)d;
        ctrl.ImageScroll.ChangeView(null, null, (float)(double)e.NewValue);
        ctrl.UpdateZoomBadge((double)e.NewValue);
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        var factor = ImageScroll.ZoomFactor;
        UpdateZoomBadge(factor);
        // Sync DP without re-triggering ChangeView
        SetValue(ZoomLevelProperty, (double)factor);
    }

    private void UpdateZoomBadge(double zoom)
        => ZoomLabel.Text = $"{zoom * 100:F0}%";

    private void UpdateVisualState()
    {
        var state = Source is null ? "NothingLoaded" : "Idle";
        VisualStateManager.GoToState(this, state, true);
    }
}
