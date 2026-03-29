using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Snaipe.Agent;

/// <summary>
/// Manages a semi-transparent highlight overlay on the target app's visual tree.
/// Injects a Canvas as the last child of the window content to draw highlight rectangles.
/// </summary>
public sealed class HighlightOverlay : IDisposable
{
    private readonly Window _window;
    private readonly ElementTracker _tracker;
    private Canvas? _overlayCanvas;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _highlightRect;
    private Panel? _injectionPanel;

    public HighlightOverlay(Window window, ElementTracker tracker)
    {
        _window = window;
        _tracker = tracker;
    }

    /// <summary>
    /// Show or hide a highlight rectangle on the element identified by <paramref name="elementId"/>.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetHighlight(string elementId, bool show)
    {
        if (!show)
        {
            HideHighlight();
            return;
        }

        if (!_tracker.TryGetElement(elementId, out var element) || element is null)
            return;

        EnsureOverlayInjected();
        if (_overlayCanvas is null || _highlightRect is null)
            return;

        try
        {
            // Get the element's bounds relative to the window root.
            var root = _window.Content as UIElement;
            if (root is null) return;

            var transform = element.TransformToVisual(root);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

            var fe = element as FrameworkElement;
            var width = fe?.ActualWidth ?? 0;
            var height = fe?.ActualHeight ?? 0;

            Canvas.SetLeft(_highlightRect, position.X);
            Canvas.SetTop(_highlightRect, position.Y);
            _highlightRect.Width = width;
            _highlightRect.Height = height;
            _highlightRect.Visibility = Visibility.Visible;
        }
        catch
        {
            // TransformToVisual can throw if the element is not in the visual tree.
            HideHighlight();
        }
    }

    private void HideHighlight()
    {
        if (_highlightRect is not null)
            _highlightRect.Visibility = Visibility.Collapsed;
    }

    private void EnsureOverlayInjected()
    {
        if (_overlayCanvas is not null)
            return;

        var content = _window.Content;
        if (content is null)
            return;

        // Ensure the content is a Panel we can add children to.
        if (content is Panel panel)
        {
            _injectionPanel = panel;
        }
        else
        {
            // Wrap existing content in a Grid.
            var grid = new Grid();
            grid.Children.Add((UIElement)content);
            _window.Content = grid;
            _injectionPanel = grid;
        }

        // Create the overlay canvas.
        _overlayCanvas = new Canvas
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        // Create the highlight rectangle.
        _highlightRect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x00, 0xA0, 0xFF)),
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x00, 0xA0, 0xFF)),
            StrokeThickness = 2,
            Visibility = Visibility.Collapsed,
        };

        _overlayCanvas.Children.Add(_highlightRect);
        _injectionPanel.Children.Add(_overlayCanvas);
    }

    public void Dispose()
    {
        if (_overlayCanvas is not null && _injectionPanel is not null)
        {
            _injectionPanel.Children.Remove(_overlayCanvas);
            _overlayCanvas = null;
            _highlightRect = null;
        }
    }
}
