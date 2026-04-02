// samples/Snaipe.SampleApp/ViewModels/AsciiOutputViewModel.cs
namespace Snaipe.SampleApp.ViewModels;

public sealed class AsciiOutputViewModel : ViewModelBase
{
    private AsciiDocument? _document;
    private ConversionStatus _displayStatus = ConversionStatus.Idle;
    private int _progressPercent;
    private string _errorMessage = string.Empty;
    private double _outputFontSize = 10.0;
    private ColorMode _colorMode = ColorMode.Grayscale;

    public AsciiDocument? Document       { get => _document;       private set => SetField(ref _document, value); }
    public ConversionStatus DisplayStatus { get => _displayStatus;  private set => SetField(ref _displayStatus, value); }
    public int ProgressPercent           { get => _progressPercent; private set => SetField(ref _progressPercent, value); }
    public string ErrorMessage           { get => _errorMessage;    private set => SetField(ref _errorMessage, value); }
    public double OutputFontSize         { get => _outputFontSize;  set => SetField(ref _outputFontSize, value); }
    public ColorMode ColorMode           { get => _colorMode;       private set => SetField(ref _colorMode, value); }

    public void UpdateFromState(ConversionState state)
    {
        Document        = state.Document;
        DisplayStatus   = state.Status;
        ProgressPercent = state.ProgressPercent;
        ErrorMessage    = state.ErrorMessage ?? string.Empty;
        ColorMode       = state.Settings.ColorMode;
    }
}
