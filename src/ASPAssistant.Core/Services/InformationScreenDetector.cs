using ASPAssistant.Core.GameModes;

namespace ASPAssistant.Core.Services;

/// <summary>Positive visual evidence for the opening, in-game info and gameplay pages.</summary>
public sealed class InformationScreenDetector(IOcrEngine ocr, IOcrStrategy strategy)
{
    public async Task<bool> IsInformationScreenAsync(byte[] screenshot, int width, int height)
    {
        var texts = await ocr.RecognizeRegionAsync(screenshot,
            (int)(width * .05), (int)(height * .13), (int)(width * .62), (int)(height * .38));
        return texts.Any(t => t.Text.Replace(" ", "").Contains("核心盟约"))
            && texts.Any(t => t.Text.Replace(" ", "").Contains("附加盟约"));
    }

    public async Task<bool> IsOpeningScreenAsync(byte[] screenshot, int width, int height)
    {
        var region = strategy.GetScanRegions().Single(r => r.Name == "BanConfirmText");
        var texts = await ocr.RecognizeRegionAsync(screenshot,
            (int)(width * region.XPercent), (int)(height * region.YPercent),
            (int)(width * region.WidthPercent), (int)(height * region.HeightPercent));
        return texts.Any(t => t.Text.Replace(" ", "").Contains("确认本局信息"))
            && await IsInformationScreenAsync(screenshot, width, height);
    }

    public async Task<bool> IsGameplayAsync(byte[] screenshot, int width, int height)
    {
        // Both cues are left of the attached panel. Neither exists on the info modal.
        var round = await ocr.RecognizeRegionAsync(screenshot,
            (int)(width * .34), 0, (int)(width * .13), (int)(height * .10));
        if (!round.Any(t => t.Text.Contains("回合"))) return false;
        var upgrade = await ocr.RecognizeRegionAsync(screenshot,
            (int)(width * .10), (int)(height * .88), (int)(width * .35), (int)(height * .09));
        return upgrade.Any(t => t.Text.Contains("升级") || t.Text.Contains("已满级"));
    }
}
