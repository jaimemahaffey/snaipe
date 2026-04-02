using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Snaipe.Protocol;
using Windows.System;

namespace Snaipe.Agent;

/// <summary>
/// Enables pick mode: while Ctrl+Shift is held over the target app, sends
/// ElementUnderCursorEvent notifications to the inspector and suppresses clicks.
///
/// Register exactly one instance per agent via Attach()/Detach().
/// All methods must be called on the UI thread.
/// </summary>
public sealed class PickModeManager
{
    /// <summary>
    /// The modifier combination that activates pick mode.
    /// Ctrl+Shift is hardcoded; make this configurable in a future change.
    /// </summary>
    private const VirtualKeyModifiers PickModifier =
        VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;

    private readonly UIElement _root;
    private readonly ElementTracker _tracker;
    private readonly HighlightOverlay _highlight;
    private readonly AgentEventServer _eventServer;

    private PointerEventHandler? _movedHandler;
    private PointerEventHandler? _pressedHandler;

    private string? _lastPickedId;
    private bool _wasPickActive;

    public PickModeManager(
        UIElement root,
        ElementTracker tracker,
        HighlightOverlay highlight,
        AgentEventServer eventServer)
    {
        _root = root;
        _tracker = tracker;
        _highlight = highlight;
        _eventServer = eventServer;
    }

    /// <summary>
    /// Register routed event handlers on the window root.
    /// handledEventsToo: true means these fire even for events already handled by child elements,
    /// ensuring pick mode intercepts events that land on popups sharing the same XamlRoot.
    /// Call once after Window.Content is set.
    /// </summary>
    public void Attach()
    {
        _movedHandler = new PointerEventHandler(OnPickPointerMoved);
        _pressedHandler = new PointerEventHandler(OnPickPointerPressed);

        _root.AddHandler(UIElement.PointerMovedEvent, _movedHandler, handledEventsToo: true);
        _root.AddHandler(UIElement.PointerPressedEvent, _pressedHandler, handledEventsToo: true);
    }

    /// <summary>
    /// Remove the routed event handlers. Call from SnaipeAgent.Dispose on the UI thread.
    /// </summary>
    public void Detach()
    {
        if (_movedHandler is not null)
            _root.RemoveHandler(UIElement.PointerMovedEvent, _movedHandler);

        if (_pressedHandler is not null)
            _root.RemoveHandler(UIElement.PointerPressedEvent, _pressedHandler);
    }

    private void OnPickPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var isPickActive = (e.KeyModifiers & PickModifier) == PickModifier;

        if (isPickActive != _wasPickActive)
        {
            _eventServer.EnqueueEvent(new PickModeActiveEvent
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Active = isPickActive,
            });

            if (!isPickActive && _lastPickedId is not null)
            {
                _highlight.SetHighlight(_lastPickedId, false);
                _lastPickedId = null;
            }
        }

        _wasPickActive = isPickActive;

        if (!isPickActive) return;

        var point = e.GetCurrentPoint(_root).Position;
        var hits = VisualTreeHelper.FindElementsInHostCoordinates(point, _root);
        var element = hits.FirstOrDefault(IsPickable);

        if (element is null) return;

        var id = _tracker.GetOrAssignId(element);
        if (id == _lastPickedId) return; // no change, avoid spamming

        _lastPickedId = id;
        _highlight.SetHighlight(id, true);

        _eventServer.EnqueueEvent(new ElementUnderCursorEvent
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ElementId = id,
            TypeName = element.GetType().Name,
        });
    }

    private void OnPickPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if ((e.KeyModifiers & PickModifier) != PickModifier) return;

        // Suppress the click so it does not reach the target app's event handlers.
        e.Handled = true;
    }

    /// <summary>
    /// Returns true for elements that are valid pick targets.
    /// Excludes the SnaipeOverlay highlight canvas and invisible elements.
    /// </summary>
    private static bool IsPickable(UIElement element) =>
        element.Visibility == Visibility.Visible &&
        element is not Microsoft.UI.Xaml.Controls.Canvas { Tag: "SnaipeOverlay" };
}
