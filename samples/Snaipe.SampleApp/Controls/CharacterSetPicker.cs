// samples/Snaipe.SampleApp/Controls/CharacterSetPicker.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Controls;

public sealed class CharacterSetPicker : Control
{
    // ── Dependency Properties ─────────────────────────────────────────

    public static readonly DependencyProperty SelectedCharacterSetProperty =
        DependencyProperty.Register(nameof(SelectedCharacterSet), typeof(CharacterSet),
            typeof(CharacterSetPicker),
            new PropertyMetadata(CharacterSet.Block, OnSelectionChanged));

    public CharacterSet SelectedCharacterSet
    {
        get => (CharacterSet)GetValue(SelectedCharacterSetProperty);
        set => SetValue(SelectedCharacterSetProperty, value);
    }

    // ── Template parts ────────────────────────────────────────────────

    private ToggleButton? _blockBtn;
    private ToggleButton? _classicBtn;
    private ToggleButton? _brailleBtn;
    private ToggleButton? _minimalBtn;

    public CharacterSetPicker()
    {
        DefaultStyleKey = typeof(CharacterSetPicker);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _blockBtn   = GetTemplateChild("PART_BlockButton")   as ToggleButton;
        _classicBtn = GetTemplateChild("PART_ClassicButton") as ToggleButton;
        _brailleBtn = GetTemplateChild("PART_BrailleButton") as ToggleButton;
        _minimalBtn = GetTemplateChild("PART_MinimalButton") as ToggleButton;

        if (_blockBtn   is not null) _blockBtn.Click   += (_, _) => SetAndSync(CharacterSet.Block);
        if (_classicBtn is not null) _classicBtn.Click += (_, _) => SetAndSync(CharacterSet.Classic);
        if (_brailleBtn is not null) _brailleBtn.Click += (_, _) => SetAndSync(CharacterSet.Braille);
        if (_minimalBtn is not null) _minimalBtn.Click += (_, _) => SetAndSync(CharacterSet.Minimal);

        SyncButtons(SelectedCharacterSet);
    }

    // ── DP change callback ────────────────────────────────────────────

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CharacterSetPicker)d).SyncButtons((CharacterSet)e.NewValue);

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetAndSync(CharacterSet value)
    {
        SelectedCharacterSet = value;
        SyncButtons(value);
    }

    private void SyncButtons(CharacterSet selected)
    {
        if (_blockBtn   is not null) _blockBtn.IsChecked   = selected == CharacterSet.Block;
        if (_classicBtn is not null) _classicBtn.IsChecked = selected == CharacterSet.Classic;
        if (_brailleBtn is not null) _brailleBtn.IsChecked = selected == CharacterSet.Braille;
        if (_minimalBtn is not null) _minimalBtn.IsChecked = selected == CharacterSet.Minimal;
    }
}
