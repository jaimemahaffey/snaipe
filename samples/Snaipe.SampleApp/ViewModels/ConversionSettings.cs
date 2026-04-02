// samples/Snaipe.SampleApp/ViewModels/ConversionSettings.cs
namespace Snaipe.SampleApp.ViewModels;

public enum CharacterSet { Block, Classic, Braille, Minimal }
public enum ColorMode { Grayscale, Ansi16, Ansi256, TrueColor }
public enum DitheringAlgorithm { None, FloydSteinberg, BayerOrdered }

public sealed record ConversionSettings(
    CharacterSet CharacterSet,
    int OutputWidth,
    ColorMode ColorMode,
    DitheringAlgorithm Dithering,
    bool Invert)
{
    public static ConversionSettings Default => new(
        CharacterSet.Block,
        OutputWidth: 120,
        ColorMode.Grayscale,
        DitheringAlgorithm.None,
        Invert: false);

    // Character arrays indexed by brightness 0–N (darkest to lightest)
    public static readonly string[] BlockChars   = [" ", "░", "▒", "▓", "█"];
    public static readonly string[] ClassicChars = [" ", ".", ":", "-", "=", "+", "*", "#", "%", "@"];
    public static readonly string[] BrailleChars = [" ", "⠂", "⠒", "⠶", "⣤", "⣶", "⣾", "⣿"];
    public static readonly string[] MinimalChars = [" ", ".", ":", "-", "=", "+", "*", "#"];

    public string[] GetChars() => CharacterSet switch
    {
        CharacterSet.Block   => BlockChars,
        CharacterSet.Classic => ClassicChars,
        CharacterSet.Braille => BrailleChars,
        CharacterSet.Minimal => MinimalChars,
        _ => BlockChars
    };
}
