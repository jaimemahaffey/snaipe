using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Snaipe.Agent;

/// <summary>
/// Maintains stable element IDs across tree walks using a <see cref="ConditionalWeakTable{TKey,TValue}"/>
/// for forward lookup (element → ID) and a <see cref="ConcurrentDictionary{TKey,TValue}"/> with
/// <see cref="WeakReference{T}"/> for reverse lookup (ID → element).
/// </summary>
public sealed class ElementTracker : IDisposable
{
    private readonly ConditionalWeakTable<UIElement, string> _elementToId = new();
    private readonly ConcurrentDictionary<string, WeakReference<UIElement>> _idToElement = new();
    private readonly Timer _sweepTimer;

    public ElementTracker()
    {
        // Sweep dead WeakReference entries every 60 seconds.
        _sweepTimer = new Timer(_ => Sweep(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Get or assign a stable GUID-based ID for an element.
    /// </summary>
    public string GetOrAssignId(UIElement element)
    {
        return _elementToId.GetValue(element, _ =>
        {
            var id = Guid.NewGuid().ToString("N");
            _idToElement[id] = new WeakReference<UIElement>(element);
            return id;
        });
    }

    /// <summary>
    /// Try to resolve an element by its ID. Returns false if the element has been GC'd or the ID is unknown.
    /// </summary>
    public bool TryGetElement(string id, out UIElement? element)
    {
        element = null;
        if (_idToElement.TryGetValue(id, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var target))
            {
                element = target;
                return true;
            }
            // Element was GC'd — clean up the stale entry.
            _idToElement.TryRemove(id, out _);
        }
        return false;
    }

    /// <summary>
    /// Rebuild the reverse lookup dictionary from a tree walk.
    /// Called after each BuildTree to ensure the reverse map is current.
    /// </summary>
    public void UpdateReverseLookup(UIElement element)
    {
        var id = GetOrAssignId(element);
        _idToElement[id] = new WeakReference<UIElement>(element);
    }

    /// <summary>
    /// Remove stale (GC'd) entries from the reverse lookup dictionary.
    /// </summary>
    private void Sweep()
    {
        foreach (var kvp in _idToElement)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                _idToElement.TryRemove(kvp.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _sweepTimer.Dispose();
    }
}
