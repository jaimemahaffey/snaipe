// samples/Snaipe.SampleApp/ViewModels/ConversionSettingsViewModel.cs
namespace Snaipe.SampleApp.ViewModels;

public sealed class ConversionSettingsViewModel : ViewModelBase
{
    private CharacterSet _characterSet = CharacterSet.Block;
    private int _outputWidth = 120;
    private ColorMode _colorMode = ColorMode.Grayscale;
    private DitheringAlgorithm _dithering = DitheringAlgorithm.None;
    private bool _invert;

    public CharacterSet CharacterSet
    {
        get => _characterSet;
        set { if (SetField(ref _characterSet, value)) NotifySettingsChanged(); }
    }
    public int OutputWidth
    {
        get => _outputWidth;
        set { if (SetField(ref _outputWidth, Math.Clamp(value, 20, 300))) NotifySettingsChanged(); }
    }
    public ColorMode ColorMode
    {
        get => _colorMode;
        set { if (SetField(ref _colorMode, value)) NotifySettingsChanged(); }
    }
    public DitheringAlgorithm Dithering
    {
        get => _dithering;
        set { if (SetField(ref _dithering, value)) NotifySettingsChanged(); }
    }
    public bool Invert
    {
        get => _invert;
        set { if (SetField(ref _invert, value)) NotifySettingsChanged(); }
    }

    public event EventHandler? SettingsChanged;

    public ConversionSettings ToSettings() =>
        new(_characterSet, _outputWidth, _colorMode, _dithering, _invert);

    private void NotifySettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
