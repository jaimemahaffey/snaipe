// samples/Snaipe.SampleApp/Controls/CharacterSetPicker.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Controls;

public sealed record CharacterSetOption(CharacterSet Value, string Label, string PreviewSample);

public sealed class CharacterSetPicker : Control
{
    public static readonly DependencyProperty SelectedCharacterSetProperty =
        DependencyProperty.Register(nameof(SelectedCharacterSet), typeof(CharacterSet), typeof(CharacterSetPicker),
            new PropertyMetadata(CharacterSet.Block, OnSelectionChanged));

    public CharacterSet SelectedCharacterSet
    {
        get => (CharacterSet)GetValue(SelectedCharacterSetProperty);
        set => SetValue(SelectedCharacterSetProperty, value);
    }

    public event EventHandler<CharacterSet>? SelectionChanged;

    private static readonly CharacterSetOption[] Options =
    [
        new(CharacterSet.Block,   "Block",   "░▒▓█ ▓▒░░\n▒▓██▓▒░░▒"),
        new(CharacterSet.Classic, "Classic", "@#$%!*+;:,\n##$$%%!!**"),
        new(CharacterSet.Braille, "Braille", "⣿⣾⣹⡏⠟⠻⢿⣿\n⣿⡿⠿⡟⠋⠉⠈⠀"),
        new(CharacterSet.Minimal, "Minimal", ". :-=+*#%@\n..::==++**"),
    ];

    // Card borders keyed by CharacterSet for highlight toggling
    private readonly Dictionary<CharacterSet, Border> _cards = new();

    public CharacterSetPicker()
    {
        DefaultStyleKey = typeof(CharacterSetPicker);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _cards.Clear();

        if (GetTemplateChild("PART_Grid") is not Grid grid) return;

        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Clear();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < Options.Length; i++)
        {
            var opt = Options[i];
            var card = BuildCard(opt);
            Grid.SetColumn(card, i % 2);
            Grid.SetRow(card, i / 2);
            grid.Children.Add(card);
            _cards[opt.Value] = card;
        }

        UpdateCardHighlights();
    }

    private Border BuildCard(CharacterSetOption opt)
    {
        var preview = new TextBlock
        {
            Text = opt.PreviewSample,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Code, Consolas, monospace"),
            FontSize = 9,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };

        var label = new TextBlock
        {
            Text = opt.Label,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 3),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray)
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6),
            Margin = new Thickness(2),
            BorderThickness = new Thickness(1),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                global::Windows.UI.Color.FromArgb(0xFF, 0x33, 0x41, 0x55)),
            Child = new StackPanel { Children = { label, preview } }
        };

        border.PointerPressed += (_, e) =>
        {
            SelectedCharacterSet = opt.Value;
            e.Handled = true;
        };

        return border;
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (CharacterSetPicker)d;
        picker.UpdateCardHighlights();
        picker.SelectionChanged?.Invoke(picker, (CharacterSet)e.NewValue);
    }

    private void UpdateCardHighlights()
    {
        foreach (var (cs, card) in _cards)
        {
            bool selected = cs == SelectedCharacterSet;
            card.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                selected
                    ? global::Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x3A, 0x8A)
                    : Microsoft.UI.Colors.Transparent);
            card.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                selected
                    ? global::Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)
                    : global::Windows.UI.Color.FromArgb(0xFF, 0x33, 0x41, 0x55));
        }
    }
}
