using Microsoft.UI.Xaml;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class ValueChainEntryViewModel
{
    public ValueChainEntryViewModel(ValueChainEntry entry)
    {
        Source = entry.Source;
        Value = entry.Value;
        IsWinner = entry.IsWinner;
    }

    public string Source { get; }
    public string Value  { get; }
    public bool   IsWinner { get; }

    /// <summary>Visible on the winning entry — shows green highlight and "wins" badge.</summary>
    public Visibility WinnerBadgeVisibility =>
        IsWinner ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible on overridden entries — shows strikethrough and dimmed opacity.</summary>
    public Visibility OverriddenVisibility =>
        IsWinner ? Visibility.Collapsed : Visibility.Visible;
}
