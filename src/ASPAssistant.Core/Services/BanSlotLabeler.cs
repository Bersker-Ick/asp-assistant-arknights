// Modified for ASP Assistant Regulation on 2026-11-11; see NOTICE.
namespace ASPAssistant.Core.Services;

/// <summary>
/// 给图标槽命名。当前用 OCR 绑定盟约名字；接入 MAA 后可以用盟约图标 TemplateMatch
/// 实现同一接口——槽位检测、完整性校验和发布流程都不需要改。
/// </summary>
public interface IBanSlotLabeler
{
    /// <summary>
    /// 为 <paramref name="pendingSlots"/> 里的槽位识别名字，返回 (槽位, 名字) 对；
    /// 识别不出的槽位不出现在结果里。
    /// </summary>
    Task<IReadOnlyList<(BanIconSlot Slot, string Name)>> LabelMissingAsync(
        byte[] screenshot,
        int imgWidth,
        int imgHeight,
        IReadOnlyList<BanIconSlot> allSlots,
        IReadOnlyList<BanIconSlot> pendingSlots,
        IReadOnlyList<string> knownNames);
}

/// <summary>
/// 现有 OCR 实现：把所有待识别槽位的名字框竖着拼成一张图，只调用一次 OCR，
/// 再按纵向位置把结果分配回各自的槽。
/// </summary>
public sealed class OcrBanSlotLabeler(IOcrEngine ocr) : IBanSlotLabeler
{
    public async Task<IReadOnlyList<(BanIconSlot Slot, string Name)>> LabelMissingAsync(
        byte[] screenshot,
        int imgWidth,
        int imgHeight,
        IReadOnlyList<BanIconSlot> allSlots,
        IReadOnlyList<BanIconSlot> pendingSlots,
        IReadOnlyList<string> knownNames)
    {
        if (pendingSlots.Count == 0 || knownNames.Count == 0)
            return [];

        double pitch = allSlots.Count > 1
            ? allSlots.Zip(allSlots.Skip(1), (a, b) => Math.Abs(b.CenterX - a.CenterX))
                .Where(d => d > 1).DefaultIfEmpty(0).Min()
            : 0;
        if (pitch <= 1)
            pitch = Math.Max(40, imgWidth * 0.06);

        var boxes = new List<(BanIconSlot Slot, int X, int Y, int W, int H)>();
        foreach (var slot in pendingSlots)
        {
            int iconHeight = Math.Max(24, slot.Bottom - slot.Top);
            int boxWidth = (int)(pitch * 0.95);
            int boxHeight = (int)(iconHeight * 0.45);
            int boxX = (int)(slot.CenterX - boxWidth / 2.0);
            int boxY = (int)(slot.Bottom + iconHeight * 0.02);
            if (boxX < 0 || boxY < 0 || boxX + boxWidth > imgWidth || boxY + boxHeight > imgHeight)
                continue;

            boxes.Add((slot, boxX, boxY, boxWidth, boxHeight));
        }

        if (boxes.Count == 0)
            return [];

        const int scale = 2;      // 名字在 1080p 上也只有 ~24 px 高，放大两倍给 OCR
        const int gap = 10;       // 行间距，避免上下两个名字被合成同一行

        System.Drawing.Bitmap? source;
        try
        {
            using var stream = new MemoryStream(screenshot);
            using var decoded = System.Drawing.Image.FromStream(stream);
            source = new System.Drawing.Bitmap(decoded);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException)
        {
            return [];
        }

        using (source)
        {
            int width = boxes.Max(b => b.W) * scale;
            int totalHeight = boxes.Sum(b => b.H * scale + gap);
            using var composite = new System.Drawing.Bitmap(width, totalHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var graphics = System.Drawing.Graphics.FromImage(composite))
            {
                graphics.Clear(System.Drawing.Color.Black);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                var offset = 0;
                foreach (var box in boxes)
                {
                    graphics.DrawImage(source,
                        new System.Drawing.Rectangle(0, offset, box.W * scale, box.H * scale),
                        new System.Drawing.Rectangle(box.X, box.Y, box.W, box.H),
                        System.Drawing.GraphicsUnit.Pixel);
                    offset += box.H * scale + gap;
                }
            }

            using var buffer = new MemoryStream();
            composite.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
            var texts = await ocr.RecognizeRegionAsync(buffer.ToArray(), 0, 0, width, totalHeight);

            var labels = new List<(BanIconSlot Slot, string Name)>();
            foreach (var text in texts)
            {
                int centerY = text.Box.Y + text.Box.H / 2;
                int offset = 0;
                foreach (var box in boxes)
                {
                    int bandHeight = box.H * scale;
                    if (centerY >= offset && centerY < offset + bandHeight)
                    {
                        var match = BanCovenantReader.MatchNameInText(text.Text, knownNames);
                        if (match is not null)
                            labels.Add((box.Slot, match));
                        break;
                    }

                    offset += bandHeight + gap;
                }
            }

            return labels;
        }
    }
}
