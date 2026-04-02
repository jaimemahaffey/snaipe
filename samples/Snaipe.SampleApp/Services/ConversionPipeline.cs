// samples/Snaipe.SampleApp/Services/ConversionPipeline.cs
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Snaipe.SampleApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinColor = Windows.UI.Color;

namespace Snaipe.SampleApp.Services;

public static class ConversionPipeline
{
    // ── Bayer 4×4 ordered dithering matrix ───────────────────────────
    private static readonly float[,] Bayer4 =
    {
        {  0f/16f,  8f/16f,  2f/16f, 10f/16f },
        { 12f/16f,  4f/16f, 14f/16f,  6f/16f },
        {  3f/16f, 11f/16f,  1f/16f,  9f/16f },
        { 15f/16f,  7f/16f, 13f/16f,  5f/16f }
    };

    // ── ANSI 16-color palette (standard terminal colors) ─────────────
    private static readonly (byte R, byte G, byte B, WinColor WinColor)[] Ansi16 =
    {
        (  0,   0,   0, FromRgb(  0,   0,   0)),  // 0  black
        (128,   0,   0, FromRgb(128,   0,   0)),  // 1  dark red
        (  0, 128,   0, FromRgb(  0, 128,   0)),  // 2  dark green
        (128, 128,   0, FromRgb(128, 128,   0)),  // 3  dark yellow
        (  0,   0, 128, FromRgb(  0,   0, 128)),  // 4  dark blue
        (128,   0, 128, FromRgb(128,   0, 128)),  // 5  dark magenta
        (  0, 128, 128, FromRgb(  0, 128, 128)),  // 6  dark cyan
        (192, 192, 192, FromRgb(192, 192, 192)),  // 7  light gray
        (128, 128, 128, FromRgb(128, 128, 128)),  // 8  dark gray
        (255,   0,   0, FromRgb(255,   0,   0)),  // 9  bright red
        (  0, 255,   0, FromRgb(  0, 255,   0)),  // 10 bright green
        (255, 255,   0, FromRgb(255, 255,   0)),  // 11 bright yellow
        (  0,   0, 255, FromRgb(  0,   0, 255)),  // 12 bright blue
        (255,   0, 255, FromRgb(255,   0, 255)),  // 13 bright magenta
        (  0, 255, 255, FromRgb(  0, 255, 255)),  // 14 bright cyan
        (255, 255, 255, FromRgb(255, 255, 255)),  // 15 white
    };

    private static WinColor FromRgb(byte r, byte g, byte b)
        => WinColor.FromArgb(255, r, g, b);

    // ── Public entry point ────────────────────────────────────────────

    public static Task<AsciiDocument> ConvertAsync(
        string imagePath,
        ConversionSettings settings,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => ConvertCore(imagePath, settings, progress, ct), ct);

    // ── Core conversion ───────────────────────────────────────────────

    private static AsciiDocument ConvertCore(
        string imagePath,
        ConversionSettings settings,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);

        // Scale width to target columns; maintain aspect ratio (chars are ~2× taller than wide)
        int targetWidth  = settings.OutputWidth;
        float aspect     = (float)image.Height / image.Width;
        int targetHeight = Math.Max(1, (int)(targetWidth * aspect * 0.5f));

        image.Mutate(x => x.Resize(targetWidth, targetHeight));

        var chars = settings.GetChars();
        ct.ThrowIfCancellationRequested();

        // Copy pixels to float arrays for dithering
        int w = image.Width, h = image.Height;
        var rCh = new float[h, w];
        var gCh = new float[h, w];
        var bCh = new float[h, w];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var px = image[x, y];
                rCh[y, x] = px.R / 255f;
                gCh[y, x] = px.G / 255f;
                bCh[y, x] = px.B / 255f;
            }

        if (settings.Dithering == DitheringAlgorithm.FloydSteinberg)
            ApplyFloydSteinberg(rCh, gCh, bCh, w, h, settings.ColorMode);
        else if (settings.Dithering == DitheringAlgorithm.BayerOrdered)
            ApplyBayer(rCh, gCh, bCh, w, h, settings.ColorMode);

        var lines = new List<AsciiLine>(h);

        for (int y = 0; y < h; y++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report((y * 100) / h);
            lines.Add(BuildLine(y, w, chars, rCh, gCh, bCh, settings));
        }

        progress?.Report(100);
        return new AsciiDocument(lines);
    }

    // ── Line builder ─────────────────────────────────────────────────

    private static AsciiLine BuildLine(
        int y, int w, string[] chars,
        float[,] rCh, float[,] gCh, float[,] bCh,
        ConversionSettings settings)
    {
        var spans = new List<AsciiSpan>(w);
        AsciiSpan? current = null;

        for (int x = 0; x < w; x++)
        {
            float r = rCh[y, x], g = gCh[y, x], b = bCh[y, x];
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            int charIdx = (int)(luma * (chars.Length - 1));
            charIdx = Math.Clamp(charIdx, 0, chars.Length - 1);
            string ch = chars[charIdx];

            WinColor? color = settings.ColorMode switch
            {
                ColorMode.Grayscale  => null,
                ColorMode.Ansi16    => NearestAnsi16(r, g, b),
                ColorMode.Ansi256   => NearestAnsi256(r, g, b),
                ColorMode.TrueColor => FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255)),
                _                   => null
            };

            // Run-length encode: merge adjacent same-color spans
            if (current is not null && current.Color == color)
            {
                current = new AsciiSpan(current.Text + ch, color);
                spans[^1] = current;
            }
            else
            {
                current = new AsciiSpan(ch, color);
                spans.Add(current);
            }
        }

        return new AsciiLine(spans);
    }

    // ── Floyd-Steinberg dithering ─────────────────────────────────────

    private static void ApplyFloydSteinberg(
        float[,] r, float[,] g, float[,] b,
        int w, int h, ColorMode mode)
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float oldR = r[y, x], oldG = g[y, x], oldB = b[y, x];
            (float nr, float ng, float nb) = Quantize(oldR, oldG, oldB, mode);
            r[y, x] = nr; g[y, x] = ng; b[y, x] = nb;

            float errR = oldR - nr, errG = oldG - ng, errB = oldB - nb;
            DiffuseError(r, g, b, w, h, x + 1, y,     errR, errG, errB, 7f / 16f);
            DiffuseError(r, g, b, w, h, x - 1, y + 1, errR, errG, errB, 3f / 16f);
            DiffuseError(r, g, b, w, h, x,     y + 1, errR, errG, errB, 5f / 16f);
            DiffuseError(r, g, b, w, h, x + 1, y + 1, errR, errG, errB, 1f / 16f);
        }
    }

    private static void DiffuseError(
        float[,] r, float[,] g, float[,] b,
        int w, int h, int x, int y,
        float errR, float errG, float errB, float weight)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        r[y, x] = Math.Clamp(r[y, x] + errR * weight, 0f, 1f);
        g[y, x] = Math.Clamp(g[y, x] + errG * weight, 0f, 1f);
        b[y, x] = Math.Clamp(b[y, x] + errB * weight, 0f, 1f);
    }

    // ── Bayer ordered dithering ───────────────────────────────────────

    private static void ApplyBayer(
        float[,] r, float[,] g, float[,] b,
        int w, int h, ColorMode mode)
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float threshold = Bayer4[y % 4, x % 4] - 0.5f;
            r[y, x] = Math.Clamp(r[y, x] + threshold * 0.5f, 0f, 1f);
            g[y, x] = Math.Clamp(g[y, x] + threshold * 0.5f, 0f, 1f);
            b[y, x] = Math.Clamp(b[y, x] + threshold * 0.5f, 0f, 1f);
        }
    }

    // ── Color quantization helpers ────────────────────────────────────

    private static (float r, float g, float b) Quantize(float r, float g, float b, ColorMode mode)
        => mode switch
        {
            ColorMode.Grayscale  => QuantizeGray(r, g, b),
            ColorMode.Ansi16    => QuantizeAnsi16(r, g, b),
            ColorMode.Ansi256   => QuantizeAnsi256(r, g, b),
            _                   => (r, g, b)
        };

    private static (float, float, float) QuantizeGray(float r, float g, float b)
    {
        float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        float q = MathF.Round(luma * 4f) / 4f;
        return (q, q, q);
    }

    private static (float, float, float) QuantizeAnsi16(float r, float g, float b)
    {
        var (idx, _) = NearestAnsi16Idx(r, g, b);
        return (Ansi16[idx].R / 255f, Ansi16[idx].G / 255f, Ansi16[idx].B / 255f);
    }

    private static (float, float, float) QuantizeAnsi256(float r, float g, float b)
    {
        // Quantize to 6-level RGB cube
        float Snap(float v) => MathF.Round(v * 5f) / 5f;
        return (Snap(r), Snap(g), Snap(b));
    }

    private static WinColor NearestAnsi16(float r, float g, float b)
    {
        var (_, color) = NearestAnsi16Idx(r, g, b);
        return color;
    }

    private static (int idx, WinColor color) NearestAnsi16Idx(float r, float g, float b)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Ansi16.Length; i++)
        {
            float dr = r - Ansi16[i].R / 255f;
            float dg = g - Ansi16[i].G / 255f;
            float db = b - Ansi16[i].B / 255f;
            float dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return (best, Ansi16[best].WinColor);
    }

    private static WinColor NearestAnsi256(float r, float g, float b)
    {
        // Map to 6-level cube
        byte Snap(float v) => (byte)(MathF.Round(v * 5f) / 5f * 255f);
        return FromRgb(Snap(r), Snap(g), Snap(b));
    }
}
