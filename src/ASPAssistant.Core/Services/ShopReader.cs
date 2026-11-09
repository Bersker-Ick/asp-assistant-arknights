// Modified for ASP Assistant Regulation on 2026-11-09; see MODIFICATIONS.md.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ASPAssistant.Core.Models;

namespace ASPAssistant.Core.Services;

/// <summary>
/// Reads the shop's bottom name strip, independently of card colour and slot count.
/// The strip ratios are measured on the Closure shop skin; other skins need samples.
/// Gold light on an operator is not evidence of an elite upgrade.
/// </summary>
public sealed class ShopReader(IOcrEngine engine, IReadOnlyList<string> catalogNames)
{
    private readonly string[] _names = catalogNames.OrderByDescending(n => n.Length).ToArray();

    public async Task<IReadOnlyList<ShopItem>> ReadAsync(
        byte[] screenshot, int width, int height, IReadOnlyList<string> trackedNames)
    {
        if (trackedNames.Count == 0)
            return [];

        // Isolate and enlarge small, dim text. Including the snowflakes below this
        // strip makes the detector merge or lose frozen card names.
        int top = (int)Math.Round(height * 11.0 / 12);
        int stripHeight = (int)Math.Round(height * 5.0 / 108);
        int canvasTop = height * 3 / 4;
        using var stream = new MemoryStream(screenshot);
        using var source = new Bitmap(stream);
        // Keep vertical padding: a very wide, 50px-high input makes PaddleOCR's
        // detector resize away the frozen text strokes.
        using var strip = new Bitmap(width * 2, (height - canvasTop) * 2, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(strip))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Black);
            graphics.DrawImage(source, new Rectangle(0, (top - canvasTop) * 2, strip.Width, stripHeight * 2),
                0, top, width, stripHeight, GraphicsUnit.Pixel);
        }
        using var encoded = new MemoryStream();
        strip.Save(encoded, ImageFormat.Png);
        var texts = await engine.RecognizeRegionAsync(encoded.ToArray(), 0, 0, strip.Width, strip.Height);
        return Match(texts, width, height, canvasTop, trackedNames);
    }

    public IReadOnlyList<ShopItem> Match(IReadOnlyList<OcrTextResult> texts,
        int width, int height, int stripTop, IReadOnlyList<string> trackedNames)
    {
        // Do not leave markers on menus/closed shops with unrelated bottom text.
        if (!texts.Any(t => t.Text.Contains("收起", StringComparison.Ordinal) && t.Box.X + t.Box.W > width * 1.6))
            return [];

        var items = new List<ShopItem>();
        var occurrences = new Dictionary<string, int>();
        foreach (var text in texts.OrderBy(t => t.Box.X))
        {
            var value = text.Text.Replace(" ", "").Replace("傑", "杰").Replace("奧", "奥");
            // Resolve against the whole catalogue before filtering tracking. Otherwise
            // tracking 维式重锤 would also match an untracked 终极维式重锤.
            var matches = new List<(string Name, int Start, int Length)>();
            foreach (var name in _names)
            {
                var candidate = name.Replace(" ", "");
                int start = value.IndexOf(candidate, StringComparison.Ordinal);
                while (start >= 0)
                {
                    if ((candidate.Length > 1 || value.Trim('M', 'X', '入', 'λ', '×') == candidate)
                        && !matches.Any(m => start < m.Start + m.Length && start + candidate.Length > m.Start))
                        matches.Add((name, start, candidate.Length));
                    start = value.IndexOf(candidate, start + candidate.Length, StringComparison.Ordinal);
                }
            }

            // Wide images can merge neighbouring labels (or 鸿雪 + 收起). Split
            // their OCR bounds by character span instead of discarding a slot.
            foreach (var match in matches.OrderBy(m => m.Start))
            {
                if (!trackedNames.Contains(match.Name))
                    continue;
                occurrences.TryGetValue(match.Name, out int occurrence);
                occurrences[match.Name] = occurrence + 1;
                items.Add(new ShopItem
                {
                    Id = $"{match.Name}@{occurrence}",
                    Name = match.Name,
                    OcrRegion = ((text.Box.X + text.Box.W * match.Start / (double)value.Length) / (2 * width),
                        (stripTop + text.Box.Y / 2.0) / height,
                        text.Box.W * match.Length / (double)value.Length / (2 * width), text.Box.H / (2.0 * height)),
                });
            }
        }
        return items;
    }
}
