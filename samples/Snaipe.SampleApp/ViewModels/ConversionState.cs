// samples/Snaipe.SampleApp/ViewModels/ConversionState.cs
namespace Snaipe.SampleApp.ViewModels;

public enum ConversionStatus { Idle, Converting, Done, Error }

public sealed record ConversionState(
    string? ImagePath,
    ConversionSettings Settings,
    AsciiDocument? Document,
    ConversionStatus Status,
    int ProgressPercent,
    string? ErrorMessage)
{
    public static ConversionState Empty => new(
        ImagePath: null,
        Settings: ConversionSettings.Default,
        Document: null,
        Status: ConversionStatus.Idle,
        ProgressPercent: 0,
        ErrorMessage: null);
}
