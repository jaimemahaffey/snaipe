// samples/Snaipe.SampleApp/SampleViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Snaipe.SampleApp;

public sealed class SampleViewModel : INotifyPropertyChanged
{
    private string _name = "Alice";
    private int _age = 30;
    private string _buttonLabel = "Click Me";
    private bool _isEnabled = true;
    private double _sliderValue = 0.8;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int Age
    {
        get => _age;
        set => SetField(ref _age, value);
    }

    public string ButtonLabel
    {
        get => _buttonLabel;
        set => SetField(ref _buttonLabel, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    public double SliderValue
    {
        get => _sliderValue;
        set
        {
            if (SetField(ref _sliderValue, value))
                OnPropertyChanged(nameof(SwatchColor));
        }
    }

    /// <summary>
    /// Interpolates between blue (#0078D4) and orange (#FF8C00) based on SliderValue (0-1).
    /// Exercises the Color/SolidColorBrush PropertyEntry path in PropertyReader.
    /// </summary>
    public SolidColorBrush SwatchColor
    {
        get
        {
            var t = Math.Clamp(_sliderValue, 0, 1);
            var r = (byte)(0x00 + t * (0xFF - 0x00));
            var g = (byte)(0x78 + t * (0x8C - 0x78));
            var b = (byte)(0xD4 + t * (0x00 - 0xD4));
            return new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
        }
    }

    public ObservableCollection<SampleItem> Items { get; } =
    [
        new("Alpha",   "100"),
        new("Beta",    "200"),
        new("Gamma",   "300"),
        new("Delta",   "400"),
        new("Epsilon", "500"),
        new("Zeta",    "600"),
        new("Eta",     "700"),
        new("Theta",   "800"),
    ];
}

public sealed record SampleItem(string Name, string Value);
