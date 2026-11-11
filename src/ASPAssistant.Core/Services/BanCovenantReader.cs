// Modified for ASP Assistant Regulation on 2026-11-09; see NOTICE.
using ASPAssistant.Core.GameModes;

namespace ASPAssistant.Core.Services;

/// <summary>An icon slot that no OCR name could be bound to.</summary>
public sealed record BanIconPosition(double X, double Y, bool IsBanned);

/// <summary>
/// Result of reading the two covenant panels: named covenants plus icons whose label could
/// not be read (reported instead of being silently dropped).
/// </summary>
public sealed record BanCovenantReadResult(
    IReadOnlyList<CovenantBanState> Core,
    IReadOnlyList<CovenantBanState> Additional,
    IReadOnlyList<BanIconPosition> UnnamedCore,
    IReadOnlyList<BanIconPosition> UnnamedAdditional,
    int CoreSlots = 0,
    int AdditionalSlots = 0,
    bool SawCoreHeader = false,
    bool SawAdditionalHeader = false)
{
    public static readonly BanCovenantReadResult Empty = new([], [], [], []);
}

/// <summary>
/// Reads the 核心盟约 / 附加盟约 panels of the "确认本局信息" screen from one screenshot.
///
/// Recognition runs in both directions:
///   icons → pixel grid (<see cref="BanIconGrid"/>) yields every slot and its state;
///   text  → OCR yields covenant names;
///   binding → each name is snapped to its nearest icon slot and the state is read from the
///   icon itself. Icons that receive no name are reported in
///   <see cref="BanCovenantReadResult.UnnamedCore"/> / <see cref="BanCovenantReadResult.UnnamedAdditional"/>.
/// </summary>
public static class BanCovenantReader
{
    public const string CoreRegionName = "BanCoreSection";
    public const string AdditionalRegionName = "BanAdditionalSection";
    public const string CoreHeaderText = "核心盟约";
    public const string AdditionalHeaderText = "附加盟约";
    public const string BanListHeaderText = "本局禁用干员";

    /// <summary>
    /// 面板级粗扫：左侧整块盟约区域。标题的位置决定两条带子，因此单人页的两行核心、
    /// 低至 y≈0.65 的附加标题都能被覆盖，不依赖每种 UI 的固定坐标。
    /// </summary>
    private static readonly (double X, double Y, double W, double H) PanelScan = (0.02, 0.13, 0.66, 0.82);

    private sealed record Candidate(OcrTextResult Ocr, string Name, int Score);

    /// <summary>
    /// Reads both covenant panels. Falls back to name-box measurement when the icon grid
    /// cannot be detected at all.
    /// </summary>
    public static async Task<BanCovenantReadResult> ReadAsync(
        IOcrEngine ocr,
        IOcrStrategy ocrStrategy,
        byte[] screenshot,
        int imgWidth,
        int imgHeight,
        IReadOnlyList<string> coreCovenantNames,
        IReadOnlyList<string> additionalCovenantNames,
        double greenThreshold = BanIconBrightness.BannedThreshold,
        IBanSlotLabeler? slotLabeler = null)
    {
        // 默认用 OCR 标签器；MAA 版本只需在调用处传入 TemplateMatch 实现。
        var labeler = slotLabeler ?? new OcrBanSlotLabeler(ocr);
        var regions = ocrStrategy.GetScanRegions();
        var coreRegion = regions.FirstOrDefault(r => r.Name == CoreRegionName);
        var additionalRegion = regions.FirstOrDefault(r => r.Name == AdditionalRegionName);

        if (coreRegion == null || additionalRegion == null)
        {
            AppLogger.Warn("BanAnalyzer",
                $"region '{CoreRegionName}'/'{AdditionalRegionName}' not found in OCR strategy");
            return BanCovenantReadResult.Empty;
        }

        // 两块区域共用一个像素缓冲：少解一次 PNG，读操作只读所以可以并行。
        using var pixels = BanScreenPixels.Load(screenshot);

        var panel = await RecognizeAsync(ocr, screenshot, imgWidth, imgHeight, PanelScan);
        var coreHeader = FindHeader(panel, CoreHeaderText);
        var additionalHeader = FindHeader(panel, AdditionalHeaderText);

        if (additionalHeader is null
            || (coreHeader is not null && additionalHeader.Box.Y <= coreHeader.Box.Y))
        {
            AppLogger.Warn("BanAnalyzer",
                "covenant headers not found in the expected order; falling back to fixed regions");
            return await ReadWithFixedRegionsAsync(ocr, screenshot, pixels, imgWidth, imgHeight,
                coreRegion, additionalRegion, coreCovenantNames, additionalCovenantNames,
                greenThreshold, labeler);
        }

        // 1080p 下 10/16 一屏放不下：为了看附加末行而向下滚动后，“核心盟约”标题会滑出视口。
        // 只要附加标题还在，就仍以附加标题为界；核心带从面板顶边开始，不要求核心标题可见。
        if (coreHeader is null && additionalHeader.Box.Y < 0.25 * imgHeight)
        {
            AppLogger.Warn("BanAnalyzer",
                "core header is missing and the additional header is too high; falling back to fixed regions");
            return await ReadWithFixedRegionsAsync(ocr, screenshot, pixels, imgWidth, imgHeight,
                coreRegion, additionalRegion, coreCovenantNames, additionalCovenantNames,
                greenThreshold, labeler);
        }

        // 核心带 = 核心标题下沿 → 附加标题上沿；附加带 = 附加标题下沿 → “本局禁用干员”标题。
        // 后者不在画面里时（单人页面板更高）用 0.96 收尾。
        var banListHeader = FindHeader(panel, BanListHeaderText);
        double coreTop;
        if (coreHeader is not null)
        {
            coreTop = Math.Clamp(
                (coreHeader.Box.Y + coreHeader.Box.H) / (double)imgHeight + 0.004, 0.05, 0.90);
        }
        else
        {
            coreTop = Math.Clamp(PanelScan.Y + 0.005, 0.05, 0.90);
            AppLogger.Info("BanAnalyzer",
                "core header is above the viewport; anchoring core band at the panel top");
        }

        double coreBottom = Math.Clamp(
            additionalHeader.Box.Y / (double)imgHeight - 0.003, coreTop + 0.03, 0.97);
        double additionalTop = Math.Clamp(
            (additionalHeader.Box.Y + additionalHeader.Box.H) / (double)imgHeight + 0.004,
            coreBottom, 0.90);
        double additionalBottom = banListHeader is null
            ? 0.96
            : banListHeader.Box.Y / (double)imgHeight - 0.004;
        additionalBottom = Math.Clamp(additionalBottom, additionalTop + 0.03, 0.99);

        AppLogger.Info("BanAnalyzer",
            $"header-anchored bands: core {coreTop:F3}-{coreBottom:F3}, " +
            $"additional {additionalTop:F3}-{additionalBottom:F3}");

        var coreLines = panel.Where(t => CenterY(t) < additionalHeader.Box.Y).ToList();
        var additionalLines = panel.Where(t => CenterY(t) >= additionalHeader.Box.Y).ToList();

        var core = await AnalyzeSectionAsync(ocr, screenshot, pixels, imgWidth, imgHeight,
            coreTop, coreBottom, coreLines, coreCovenantNames, "Core", greenThreshold, labeler);
        var additional = await AnalyzeSectionAsync(ocr, screenshot, pixels, imgWidth, imgHeight,
            additionalTop, additionalBottom, additionalLines, additionalCovenantNames,
            "Additional", greenThreshold, labeler);

        return new BanCovenantReadResult(
            core.Covenants, additional.Covenants, core.Unnamed, additional.Unnamed,
            core.Slots, additional.Slots, SawCoreHeader: coreHeader is not null, SawAdditionalHeader: true);
    }

    private sealed record SectionRead(
        IReadOnlyList<CovenantBanState> Covenants, IReadOnlyList<BanIconPosition> Unnamed,
        int Slots = 0, bool SawHeader = false);

    /// <summary>
    /// 固定区域回退：标题缺失（面板被滚动）时仍按旧坐标读取，保证不倒退。
    /// </summary>
    private static async Task<BanCovenantReadResult> ReadWithFixedRegionsAsync(
        IOcrEngine ocr, byte[] screenshot, BanScreenPixels? pixels, int imgWidth, int imgHeight,
        OcrRegionDefinition coreRegion, OcrRegionDefinition additionalRegion,
        IReadOnlyList<string> coreCovenantNames, IReadOnlyList<string> additionalCovenantNames,
        double greenThreshold, IBanSlotLabeler labeler)
    {
        var coreTask = ReadSectionAsync(ocr, screenshot, pixels, imgWidth, imgHeight, coreRegion,
            CoreHeaderText, coreCovenantNames, "Core", greenThreshold, labeler);
        var additionalTask = ReadSectionAsync(ocr, screenshot, pixels, imgWidth, imgHeight, additionalRegion,
            AdditionalHeaderText, additionalCovenantNames, "Additional", greenThreshold, labeler);

        await Task.WhenAll(coreTask, additionalTask);
        var core = await coreTask;
        var additional = await additionalTask;

        return new BanCovenantReadResult(
            core.Covenants, additional.Covenants, core.Unnamed, additional.Unnamed,
            core.Slots, additional.Slots, core.SawHeader, additional.SawHeader);
    }

    private static Task<IReadOnlyList<OcrTextResult>> RecognizeAsync(
        IOcrEngine ocr, byte[] screenshot, int imgWidth, int imgHeight,
        (double X, double Y, double W, double H) region) =>
        ocr.RecognizeRegionAsync(screenshot,
            (int)(region.X * imgWidth), (int)(region.Y * imgHeight),
            (int)(region.W * imgWidth), (int)(region.H * imgHeight));

    private static OcrTextResult? FindHeader(IReadOnlyList<OcrTextResult> lines, string headerText) =>
        lines.Where(t => NormalizeHeader(t.Text).Contains(headerText))
            .OrderBy(t => t.Box.Y)
            .FirstOrDefault();

    private static async Task<SectionRead> ReadSectionAsync(
        IOcrEngine ocr, byte[] screenshot, BanScreenPixels? pixels, int imgWidth, int imgHeight,
        OcrRegionDefinition region, string headerText,
        IReadOnlyList<string> knownNames, string group, double greenThreshold,
        IBanSlotLabeler labeler)
    {
        int rx = (int)(region.XPercent * imgWidth);
        int ry = (int)(region.YPercent * imgHeight);
        int rw = (int)(region.WidthPercent * imgWidth);
        int rh = (int)(region.HeightPercent * imgHeight);

        if (rw <= 0 || rh <= 0)
        {
            AppLogger.Warn("BanAnalyzer", $"{group}: region has zero size");
            return new SectionRead([], []);
        }

        var ocrResults = await ocr.RecognizeRegionAsync(screenshot, rx, ry, rw, rh);
        AppLogger.Info("BanAnalyzer",
            $"{group} raw OCR ({ocrResults.Count}): [{string.Join(", ", ocrResults.Select(r => r.Text))}]");

        var sawHeader = ocrResults.Any(r => NormalizeHeader(r.Text).Contains(headerText));
        if (!sawHeader)
        {
            AppLogger.Warn("BanAnalyzer",
                $"{group}: header '{headerText}' missing from region {region.Name} (page may be scrolled)");
        }

        var section = await AnalyzeSectionAsync(ocr, screenshot, pixels, imgWidth, imgHeight,
            region.YPercent, region.YPercent + region.HeightPercent, ocrResults, knownNames,
            group, greenThreshold, labeler);

        return section with { SawHeader = sawHeader };
    }

    /// <summary>
    /// 在给定的 y 区间内找图标槽并绑定名字。OCR 结果由调用方提供：
    /// 标题锚定路径复用面板级扫描，固定区域回退路径使用各自区域的扫描。
    /// </summary>
    private static async Task<SectionRead> AnalyzeSectionAsync(
        IOcrEngine ocr, byte[] screenshot, BanScreenPixels? pixels, int imgWidth, int imgHeight,
        double minY, double maxY, IReadOnlyList<OcrTextResult> ocrResults,
        IReadOnlyList<string> knownNames, string group, double greenThreshold,
        IBanSlotLabeler labeler)
    {
        var slots = pixels is null
            ? BanIconGrid.Detect(screenshot, imgWidth, imgHeight, minY, maxY)
            : BanIconGrid.Detect(pixels, minY, maxY);

        var candidates = MatchCandidates(ocrResults, knownNames, imgWidth);

        if (slots.Count == 0)
        {
            if (candidates.Count == 0)
                return new SectionRead([], []);

            return MeasureFromNameBoxes(screenshot, imgWidth, imgHeight, candidates, group, greenThreshold, pixels);
        }

        var (bound, unbound) = AssignToSlots(candidates, slots);

        // 整行 OCR 偶尔读不出名字（缩放过、被暗化、字体发糊），但图标位置是稳定的。
        // 只对“确实还没绑上名字”的槽做兜底；两行布局里同一 X 有上下两个槽，
        // 因此不能像旧实现那样按 X 判重，否则第二行永远不会补名称。
        var pending = slots.Where(s => !bound.ContainsKey(s)).ToList();
        if (pending.Count > 0 && knownNames.Count > 0)
        {
            var labels = await labeler.LabelMissingAsync(
                screenshot, imgWidth, imgHeight, slots, pending, knownNames);

            foreach (var (slot, name) in labels)
            {
                // 按行号 + 中心点匹配，而不是依赖 record 的引用/精确相等：
                // 下一期 MAA 标签器可能会重建槽位对象。
                var target = pending.FirstOrDefault(p =>
                    p.Row == slot.Row && Math.Abs(p.CenterX - slot.CenterX) <= 1.0);
                if (target is null || bound.ContainsKey(target))
                    continue;
                if (bound.Values.Any(c => c.Name == name))
                {
                    AppLogger.Warn("BanAnalyzer", $"{group}: duplicate slot label '{name}' ignored");
                    continue;
                }

                bound[target] = new Candidate(
                    new OcrTextResult(name, ((int)target.CenterX, target.Top, 1, Math.Max(1, target.Bottom - target.Top))),
                    name, int.MaxValue);
            }

            AppLogger.Info("BanAnalyzer", $"{group}: slot OCR recovered {bound.Count}/{slots.Count} names");
        }

        return BuildSectionRead(bound, unbound, slots, group, greenThreshold) with { Slots = slots.Count };
    }

    /// <summary>
    /// 把一整行文本匹配到候选盟约名（供槽位标签器的 OCR 实现复用）。
    /// </summary>
    public static string? MatchNameInText(string text, IReadOnlyList<string> knownNames)
    {
        var best = MatchLine(new OcrTextResult(text, (0, 0, 0, 0)), knownNames)
            .OrderByDescending(c => c.Score)
            .FirstOrDefault();
        return best?.Name;
    }

    /// <summary>
    /// Primary path: every name is snapped to the nearest icon slot. Slots without a name
    /// are left for the slot labeler.
    /// </summary>
    private static (Dictionary<BanIconSlot, Candidate> Bound, List<Candidate> Unbound) AssignToSlots(
        List<Candidate> candidates, IReadOnlyList<BanIconSlot> slots)
    {
        var tolerances = slots
            .GroupBy(s => s.Row)
            .ToDictionary(g => g.Key, g => SlotTolerance(g.Select(s => s.CenterX).ToList()));

        var bound = new Dictionary<BanIconSlot, Candidate>();
        var unbound = new List<Candidate>();

        foreach (var candidate in candidates)
        {
            double cx = CenterX(candidate.Ocr);
            double cy = CenterY(candidate.Ocr);

            // Rows share X positions, so the vertical distance decides which row a name
            // belongs to (the label always sits below its own icon).
            var slot = slots
                .Where(s => Math.Abs(s.CenterX - cx) <= tolerances[s.Row])
                .OrderBy(s => Math.Abs(s.CenterY - cy))
                .ThenBy(s => Math.Abs(s.CenterX - cx))
                .FirstOrDefault();

            if (slot == null)
            {
                unbound.Add(candidate);
                continue;
            }

            if (!bound.TryGetValue(slot, out var existing) || IsBetter(candidate, existing))
                bound[slot] = candidate;
        }

        return (bound, unbound);
    }

    /// <summary>
    /// Turns the slot → name binding into the public result: one state per named icon plus
    /// one position for every icon that stayed unnamed.
    /// </summary>
    private static SectionRead BuildSectionRead(
        Dictionary<BanIconSlot, Candidate> bound, List<Candidate> unbound,
        IReadOnlyList<BanIconSlot> slots, string group, double greenThreshold)
    {
        var states = new List<CovenantBanState>(bound.Count);
        foreach (var (slot, candidate) in bound)
        {
            bool banned = slot.GreenFraction < greenThreshold;
            AppLogger.Info("BanAnalyzer",
                $"{group} '{candidate.Name}' (ocr='{candidate.Ocr.Text}') slot x≈{slot.CenterX:F0} " +
                $"green={slot.GreenFraction:F3} banned={banned}");
            states.Add(new CovenantBanState(candidate.Name, banned, slot.CenterX));
        }

        var unnamed = slots
            .Where(s => !bound.ContainsKey(s))
            .Select(s => new BanIconPosition(s.CenterX, s.CenterY, s.GreenFraction < greenThreshold))
            .OrderBy(p => p.Y)
            .ThenBy(p => p.X)
            .ToList();

        if (unbound.Count > 0)
        {
            AppLogger.Warn("BanAnalyzer",
                $"{group}: {unbound.Count} name(s) matched no icon slot: " +
                $"[{string.Join(", ", unbound.Select(c => $"{c.Name}@{CenterX(c.Ocr):F0}"))}]");
        }

        if (unnamed.Count > 0)
        {
            AppLogger.Warn("BanAnalyzer",
                $"{group}: {unnamed.Count} icon(s) without a name: " +
                $"[{string.Join(", ", unnamed.Select(p => $"x≈{p.X:F0}{(p.IsBanned ? "/禁用" : "/可用")}"))}]");
        }

        return new SectionRead(states.OrderBy(s => s.X).ToList(), unnamed);
    }

    /// <summary>
    /// Fallback path used when the icon grid is not detected: measure each name's own box.
    /// Nothing is dropped, but unnamed icons cannot be reported.
    /// </summary>
    private static SectionRead MeasureFromNameBoxes(
        byte[] screenshot, int imgWidth, int imgHeight,
        List<Candidate> candidates, string group, double greenThreshold, BanScreenPixels? pixels = null)
    {
        AppLogger.Warn("BanAnalyzer", $"{group}: icon grid not found, falling back to name boxes");

        var names = candidates.Select(c => c.Ocr).ToList();
        var fractions = pixels is null
            ? BanIconBrightness.MeasureGreenFractions(screenshot, imgWidth, imgHeight, names)
            : BanIconBrightness.MeasureGreenFractions(pixels, imgWidth, imgHeight, names);

        var states = new List<CovenantBanState>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            double cx = CenterX(candidates[i].Ocr);
            bool banned = fractions[i] < greenThreshold;
            AppLogger.Info("BanAnalyzer",
                $"{group} '{candidates[i].Name}' (ocr='{candidates[i].Ocr.Text}') box x≈{cx:F0} y={candidates[i].Ocr.Box.Y} " +
                $"green={fractions[i]:F3} banned={banned}");
            states.Add(new CovenantBanState(candidates[i].Name, banned, cx));
        }

        return new SectionRead(states.OrderBy(s => s.X).ToList(), []);
    }

    /// <summary>
    /// Names may sit up to half a column away from their icon; derive that tolerance from the
    /// slot spacing instead of a hard-coded pixel value.
    /// </summary>
    private static double SlotTolerance(List<double> centers)
    {
        if (centers.Count < 2)
            return 58;

        var sorted = centers.OrderBy(c => c).ToList();
        var gaps = new List<double>();
        for (int i = 1; i < sorted.Count; i++)
            gaps.Add(sorted[i] - sorted[i - 1]);
        gaps.Sort();

        double median = gaps[gaps.Count / 2];
        return Math.Max(20, median * 0.5);
    }

    /// <summary>
    /// Maps OCR output to covenant names and collapses fragments that belong to the same
    /// icon slot, so one covenant is reported once even when OCR splits or merges its label.
    /// </summary>
    private static List<Candidate> MatchCandidates(
        IReadOnlyList<OcrTextResult> ocrResults, IReadOnlyList<string> knownNames, int imgWidth)
    {
        if (knownNames.Count == 0)
            return [];

        var matches = new List<Candidate>();
        foreach (var result in ocrResults)
            matches.AddRange(MatchLine(result, knownNames));

        if (matches.Count <= 1)
            return matches;

        // Fragments of the same icon are in the same visual row and within half a column
        // pitch of each other. Rows matter: the additional panel has two rows whose columns
        // share X positions, so collapsing on X alone would drop the second row.
        matches.Sort((a, b) =>
        {
            int byRow = CenterY(a.Ocr).CompareTo(CenterY(b.Ocr));
            return byRow != 0 ? byRow : CenterX(a.Ocr).CompareTo(CenterX(b.Ocr));
        });

        double toleranceX = Math.Max(24, imgWidth * 0.03);

        var collapsed = new List<Candidate> { matches[0] };
        for (int i = 1; i < matches.Count; i++)
        {
            var current = matches[i];
            var last = collapsed[^1];
            double toleranceY = Math.Max(4, 0.6 * Math.Max(last.Ocr.Box.H, current.Ocr.Box.H));

            bool sameSlot = Math.Abs(CenterX(current.Ocr) - CenterX(last.Ocr)) < toleranceX
                            && Math.Abs(CenterY(current.Ocr) - CenterY(last.Ocr)) <= toleranceY;

            if (sameSlot)
            {
                if (IsBetter(current, last))
                    collapsed[^1] = current;
            }
            else
            {
                collapsed.Add(current);
            }
        }

        return collapsed;
    }

    private static bool IsBetter(Candidate candidate, Candidate current) =>
        candidate.Score > current.Score
        || (candidate.Score == current.Score && candidate.Ocr.Text.Length > current.Ocr.Text.Length);

    private static double CenterX(OcrTextResult ocr) => ocr.Box.X + ocr.Box.W / 2.0;

    /// <summary>标题里的「盟」在低分辨率/抗锯齿下常被读成「明」。</summary>
    private static string NormalizeHeader(string text) =>
        text.Replace(" ", "").Replace("明", "盟").Replace("約", "约");

    private static double CenterY(OcrTextResult ocr) => ocr.Box.Y + ocr.Box.H / 2.0;

    // ── Covenant name matching ────────────────────────────────────────────────

    /// <summary>
    /// Fragments that mark a line as a section title / warning instead of a covenant name.
    /// Matched loosely, these used to be mapped onto real covenants ("本局禁用干员" → "协防干员").
    /// </summary>
    private static readonly string[] SectionNoiseFragments =
    [
        "本局禁用", "部分盟约", "所含", "阵容不完整", "核心盟约", "附加盟约",
        "已就绪", "准备就绪", "绝境模拟", "确认本局",
    ];

    /// <summary>
    /// Maps one OCR line to one or more covenant candidates.
    ///
    /// OCR regularly merges two neighbouring labels into a single line
    /// ("伊比利亚维多利亚"); mapping such a line to just one name used to hide the other
    /// covenant. The line is therefore split at the real character positions and every
    /// matched name keeps its own slice of the box, so each name snaps to its own icon.
    ///
    /// Order: exact → names contained in the line → strict character-overlap fallback.
    /// Scores: 3 exact, 2 contained, 1 fuzzy.
    /// </summary>
    private static List<Candidate> MatchLine(OcrTextResult ocr, IReadOnlyList<string> knownNames)
    {
        if (knownNames.Count == 0) return [];

        // Real opening capture: PaddleOCR drops 比 inside a merged covenant-name line.
        var clean = ocr.Text.Trim()
            .Replace("伊利亚", "伊比利亚", StringComparison.Ordinal)
            // The live 4K frame occasionally drops 戈 from 阿戈尔 inside a merged line.
            .Replace("阿尔", "阿戈尔", StringComparison.Ordinal);
        if (clean.Length == 0) return [];
        if (SectionNoiseFragments.Any(f => clean.Contains(f, StringComparison.Ordinal))) return [];

        if (knownNames.Contains(clean)) return [new Candidate(ocr, clean, 3)];

        var contained = FindContainedNames(clean, knownNames);
        if (contained.Count > 0)
        {
            return contained
                .Select(c => new Candidate(
                    contained.Count == 1 && c.Name.Length == clean.Length
                        ? ocr
                        : SliceBox(ocr, c.Start, c.Name.Length, clean.Length),
                    c.Name,
                    2))
                .ToList();
        }

        // Fuzzy fallback for single-character OCR mistakes. Requires most of the name to
        // survive; the previous 50 % rule was loose enough to match section titles.
        if (clean.Length < 2) return [];

        string? best = null;
        int bestScore = 0;
        foreach (var candidate in knownNames)
        {
            int shorter = Math.Min(clean.Length, candidate.Length);
            if (shorter < 2) continue;
            int shared = SharedCharCount(clean, candidate);
            if (shared * 5 < shorter * 4) continue;
            if (shared > bestScore)
            {
                bestScore = shared;
                best = candidate;
            }
        }

        return best == null ? [] : [new Candidate(ocr, best, 1)];
    }

    /// <summary>
    /// Finds every known name contained in <paramref name="text"/>, scanning left to right
    /// and taking the longest name at each position, so "伊比利亚维多利亚" yields both.
    /// </summary>
    private static List<(string Name, int Start)> FindContainedNames(
        string text, IReadOnlyList<string> knownNames)
    {
        var found = new List<(string Name, int Start)>();
        int index = 0;
        while (index < text.Length)
        {
            string? bestName = null;
            foreach (var name in knownNames)
            {
                if (name.Length < 2) continue;
                if (text.AsSpan(index).StartsWith(name, StringComparison.Ordinal)
                    && (bestName == null || name.Length > bestName.Length))
                {
                    bestName = name;
                }
            }

            if (bestName == null)
            {
                index++;
                continue;
            }

            found.Add((bestName, index));
            index += bestName.Length;
        }

        return found;
    }

    private static OcrTextResult SliceBox(OcrTextResult ocr, int start, int length, int totalLength)
    {
        double charWidth = (double)ocr.Box.W / Math.Max(1, totalLength);
        int x = ocr.Box.X + (int)Math.Round(charWidth * start);
        int w = Math.Max(1, (int)Math.Round(charWidth * length));
        return ocr with { Box = (x, ocr.Box.Y, w, ocr.Box.H) };
    }

    private static int SharedCharCount(string a, string b)
    {
        var freq = new Dictionary<char, int>();
        foreach (var ch in b)
            freq[ch] = freq.GetValueOrDefault(ch) + 1;

        int shared = 0;
        foreach (var ch in a)
        {
            if (freq.TryGetValue(ch, out int count) && count > 0)
            {
                shared++;
                freq[ch] = count - 1;
            }
        }
        return shared;
    }
}
