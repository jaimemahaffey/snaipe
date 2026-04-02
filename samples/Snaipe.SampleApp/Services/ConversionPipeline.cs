// samples/Snaipe.SampleApp/Services/ConversionPipeline.cs
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Services;

public sealed class ConversionPipeline : IDisposable
{
    private CancellationTokenSource? _cts;
    private Image<Rgba32>? _loadedImage;
    private string? _loadedPath;

    // ── ANSI 16-color palette (approximated as RGB) ───────────────────
    private static readonly (global::Windows.UI.Color Color, string Name)[] Ansi16 =
    [
        (global::Windows.UI.Color.FromArgb(0xFF,   0,   0,   0), "Black"),
        (global::Windows.UI.Color.FromArgb(0xFF, 128,   0,   0), "DarkRed"),
        (global::Windows.UI.Color.FromArgb(0xFF,   0, 128,   0), "DarkGreen"),
        (global::Windows.UI.Color.FromArgb(0xFF, 128, 128,   0), "DarkYellow"),
        (global::Windows.UI.Color.FromArgb(0xFF,   0,   0, 128), "DarkBlue"),
        (global::Windows.UI.Color.FromArgb(0xFF, 128,   0, 128), "DarkMagenta"),
        (global::Windows.UI.Color.FromArgb(0xFF,   0, 128, 128), "DarkCyan"),
        (global::Windows.UI.Color.FromArgb(0xFF, 192, 192, 192), "Gray"),
        (global::Windows.UI.Color.FromArgb(0xFF, 128, 128, 128), "DarkGray"),
        (global::Windows.UI.Color.FromArgb(0xFF, 255,   0,   0), "Red"),
        (global::Windows.UI.Color.FromArgb(0xFF,   0, 255,   0), "Green"),
        (global::Windows.UI.Color.FromArgb(0xFF, 255, 255,   0), "Yellow"),
        (global::Windows.UI.Color.FromArgb(0xFF,   0,   0, 255), "Blue"),
        (global::Windows.UI.Color.FromArgb(0xFF, 255,   0, 255), "Magenta"),
        (global::Windows.UI.Color.FromArgb(0xFF,   0, 255, 255), "Cyan"),
        (global::Windows.UI.Color.FromArgb(0xFF, 255, 255, 255), "White"),
    ];

    public async Task<AsciiDocument> ConvertAsync(
        string imagePath,
        ConversionSettings settings,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        // Load image (cache if same path)
        if (_loadedPath != imagePath)
        {
            _loadedImage?.Dispose();
            _loadedImage = await Image.LoadAsync<Rgba32>(imagePath, ct);
            _loadedPath = imagePath;
        }

        var src = _loadedImage!;

        // Resize: ASCII chars are roughly 2× taller than wide, so halve the height
        const double aspectCorrection = 0.5;
        int targetW = Math.Max(1, settings.OutputWidth);
        int targetH = Math.Max(1, (int)(src.Height * ((double)targetW / src.Width) * aspectCorrection));

        using var resized = src.Clone(ctx => ctx.Resize(targetW, targetH));

        var chars = settings.GetChars();
        var lines = new List<AsciiLine>(targetH);

        // Build dithering error buffer for Floyd-Steinberg
        var errorR = new double[targetH, targetW];
        var errorG = new double[targetH, targetW];
        var errorB = new double[targetH, targetW];

        // Bayer 4×4 matrix (values 0–15, threshold at 8)
        int[,] bayer4 = {
            {  0,  8,  2, 10 },
            { 12,  4, 14,  6 },
            {  3, 11,  1,  9 },
            { 15,  7, 13,  5 }
        };

        for (int row = 0; row < targetH; row++)
        {
            ct.ThrowIfCancellationRequested();

            var spans = new List<AsciiSpan>();
            string? currentText = null;
            global::Windows.UI.Color? currentColor = null;
            var runBuffer = new System.Text.StringBuilder();

            for (int col = 0; col < targetW; col++)
            {
                var pixel = resized[col, row];

                double r = pixel.R / 255.0;
                double g = pixel.G / 255.0;
                double b = pixel.B / 255.0;

                // Apply accumulated dithering error
                if (settings.Dithering == DitheringAlgorithm.FloydSteinberg)
                {
                    r = Math.Clamp(r + errorR[row, col], 0, 1);
                    g = Math.Clamp(g + errorG[row, col], 0, 1);
                    b = Math.Clamp(b + errorB[row, col], 0, 1);
                }
                else if (settings.Dithering == DitheringAlgorithm.BayerOrdered)
                {
                    double threshold = bayer4[row % 4, col % 4] / 16.0 - 0.5;
                    r = Math.Clamp(r + threshold * 0.25, 0, 1);
                    g = Math.Clamp(g + threshold * 0.25, 0, 1);
                    b = Math.Clamp(b + threshold * 0.25, 0, 1);
                }

                double brightness = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                if (settings.Invert) brightness = 1 - brightness;

                int charIndex = (int)(brightness * (chars.Length - 1));
                charIndex = Math.Clamp(charIndex, 0, chars.Length - 1);
                string ch = chars[charIndex];

                // Propagate Floyd-Steinberg error
                if (settings.Dithering == DitheringAlgorithm.FloydSteinberg)
                {
                    double quantR = brightness - (double)charIndex / (chars.Length - 1);
                    PropagateFSError(errorR, row, col, targetW, targetH, r - (r - quantR * 0.5));
                    PropagateFSError(errorG, row, col, targetW, targetH, g - (g - quantR * 0.5));
                    PropagateFSError(errorB, row, col, targetW, targetH, b - (b - quantR * 0.5));
                }

                // Determine color for this character
                global::Windows.UI.Color? spanColor = settings.ColorMode switch
                {
                    ColorMode.Grayscale => null,
                    ColorMode.Ansi16    => NearestAnsi16(r, g, b),
                    ColorMode.Ansi256   => Ansi256Color(r, g, b),
                    ColorMode.TrueColor => global::Windows.UI.Color.FromArgb(0xFF,
                        (byte)(r * 255), (byte)(g * 255), (byte)(b * 255)),
                    _ => null
                };

                // Run-length encode: flush run if color changes
                if (spanColor != currentColor && runBuffer.Length > 0)
                {
                    spans.Add(new AsciiSpan(runBuffer.ToString(), currentColor));
                    runBuffer.Clear();
                }
                currentColor = spanColor;
                runBuffer.Append(ch);
            }

            if (runBuffer.Length > 0)
                spans.Add(new AsciiSpan(runBuffer.ToString(), currentColor));

            lines.Add(new AsciiLine(spans));
            progress?.Report((row + 1) * 100 / targetH);
        }

        return new AsciiDocument(lines);
    }

    private static void PropagateFSError(double[,] err, int row, int col, int w, int h, double e)
    {
        if (col + 1 < w)         err[row,     col + 1] += e * 7 / 16.0;
        if (row + 1 < h)
        {
            if (col > 0)         err[row + 1, col - 1] += e * 3 / 16.0;
                                 err[row + 1, col    ] += e * 5 / 16.0;
            if (col + 1 < w)     err[row + 1, col + 1] += e * 1 / 16.0;
        }
    }

    private static global::Windows.UI.Color NearestAnsi16(double r, double g, double b)
    {
        double minDist = double.MaxValue;
        global::Windows.UI.Color best = Ansi16[0].Color;
        foreach (var (color, _) in Ansi16)
        {
            double dr = r - color.R / 255.0;
            double dg = g - color.G / 255.0;
            double db = b - color.B / 255.0;
            double dist = dr * dr + dg * dg + db * db;
            if (dist < minDist) { minDist = dist; best = color; }
        }
        return best;
    }

    private static global::Windows.UI.Color Ansi256Color(double r, double g, double b)
    {
        // Map to 6×6×6 color cube (indices 16–231)
        int ri = (int)(r * 5 + 0.5);
        int gi = (int)(g * 5 + 0.5);
        int bi = (int)(b * 5 + 0.5);
        byte rv = (byte)(ri == 0 ? 0 : 55 + ri * 40);
        byte gv = (byte)(gi == 0 ? 0 : 55 + gi * 40);
        byte bv = (byte)(bi == 0 ? 0 : 55 + bi * 40);
        return global::Windows.UI.Color.FromArgb(0xFF, rv, gv, bv);
    }

    public void Cancel() => _cts?.Cancel();

    public void Dispose()
    {
        _cts?.Dispose();
        _loadedImage?.Dispose();
    }
}
