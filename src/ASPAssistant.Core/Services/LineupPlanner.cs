using ASPAssistant.Core.Models;

namespace ASPAssistant.Core.Services;

/// <summary>
/// Searches compact lineups (8/9 people × 0/1/2 transforms) that reach the selected covenant targets.
/// Only operators the targets actually need are picked; the rest of the lineup is reported as free slots.
/// </summary>
public sealed class LineupPlanner
{
    /// <summary>Covenants the 变形同构体 transform cannot grant.</summary>
    public static readonly IReadOnlySet<string> NoTransform = new HashSet<string> { "灵巧", "协防干员", "奇迹", "远见", "独行", "绝技", "调和" };
    /// <summary>Additional covenants whose level II is simply "3 operators".</summary>
    public static readonly IReadOnlySet<string> PeopleSecond = new HashSet<string> { "精准", "坚守", "助力", "不屈", "聚合", "奥术" };
    /// <summary>Covenants without a level II.</summary>
    public static readonly IReadOnlySet<string> NoSecond = new HashSet<string> { "独行", "调和", "协防干员" };
    public const string SoloCovenant = "独行";
    public const int MaxLockedOperators = 9;
    private const int MaxBranches = 48;

    private readonly IReadOnlyList<Operator> _operators;
    private readonly IReadOnlyList<CovenantInfo> _covenants;
    private readonly HashSet<string> _coreNames;

    public LineupPlanner(IEnumerable<Operator> operators, IEnumerable<CovenantInfo> covenants)
    {
        _operators = [.. operators];
        _covenants = [.. covenants];
        _coreNames = _covenants.Where(c => c.BondType == "SEASON").Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<CovenantInfo> Covenants => _covenants;

    // ── Goal helpers (shared with the UI) ───────────────────────────────────

    public static bool HasSecondLevel(CovenantInfo covenant) => !NoSecond.Contains(covenant.Name);

    /// <summary>True when level II depends on stacks / elites / other conditions the planner cannot verify.</summary>
    public static bool IsSecondLevelManual(CovenantInfo covenant)
        => covenant.BondType != "SEASON" && HasSecondLevel(covenant) && !PeopleSecond.Contains(covenant.Name);

    public static PlannerGoal CreateGoal(CovenantInfo covenant, int level)
    {
        var isCore = covenant.BondType == "SEASON";
        var basic = covenant.ActivateCount ?? (isCore ? 3 : 2);
        if (level < 2 || !HasSecondLevel(covenant)) return new PlannerGoal(covenant.Name, 1, basic, false, isCore);
        if (isCore) return new PlannerGoal(covenant.Name, 2, 6, false, true);
        if (PeopleSecond.Contains(covenant.Name)) return new PlannerGoal(covenant.Name, 2, 3, false, false);
        return new PlannerGoal(covenant.Name, 2, basic, true, false);
    }

    public IReadOnlySet<string> DefaultTransformAllowed(IReadOnlySet<string>? bannedCovenants = null)
        => _covenants.Where(c => !NoTransform.Contains(c.Name) && bannedCovenants?.Contains(c.Name) != true)
            .Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Operators that cannot appear because of covenant bans: their only covenant is banned,
    /// or both a core covenant and an additional covenant of theirs are banned.
    /// </summary>
    public bool IsExcludedByBans(Operator op, IReadOnlySet<string> bannedCovenants)
    {
        if (bannedCovenants.Count == 0) return false;
        var all = op.CoreCovenants.Concat(op.AdditionalCovenants).Distinct().ToList();
        if (all.Count == 1 && bannedCovenants.Contains(all[0])) return true;
        var coreBanned = all.Any(c => _coreNames.Contains(c) && bannedCovenants.Contains(c));
        var additionalBanned = all.Any(c => !_coreNames.Contains(c) && bannedCovenants.Contains(c));
        return coreBanned && additionalBanned;
    }

    // ── Search ──────────────────────────────────────────────────────────────

    /// <summary>Backwards-compatible flat list of every lineup found.</summary>
    public IReadOnlyList<PlannedLineup> Solve(PlannerRequest request, int maxResultsPerVariant = 3, int maxNodes = 40000)
        => Plan(request, CancellationToken.None, maxResultsPerVariant, maxNodes).AllLineups.ToList();

    public PlannerResult Plan(PlannerRequest request, CancellationToken cancellationToken = default,
        int maxResultsPerVariant = 3, int maxNodes = 40000)
    {
        if (request.Goals.Count == 0) return new PlannerResult();
        // A banned additional covenant only removes operators whose covenants are all banned; the rest can still
        // reach its activation count, so only banned core covenants are rejected as targets.
        var bannedGoal = request.Goals.FirstOrDefault(g => g.IsCore && request.BannedCovenants.Contains(g.Name));
        if (bannedGoal is not null) return new PlannerResult { Error = $"核心盟约“{bannedGoal.Name}”本局已被 Ban。" };
        if (request.LockedOperators.Count > MaxLockedOperators) return new PlannerResult { Error = $"必带干员不能超过 {MaxLockedOperators} 人。" };

        var candidates = _operators
            .Where(o => !request.BannedOperators.Contains(o.Name))
            .Where(o => !request.ExcludeMixedBannedOperators || !IsExcludedByBans(o, request.BannedCovenants))
            .ToList();
        var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < candidates.Count; i++) indexByName.TryAdd(candidates[i].Name, i);

        var missingLocked = request.LockedOperators.FirstOrDefault(n => !indexByName.ContainsKey(n));
        if (missingLocked is not null)
            return new PlannerResult { Error = $"必带干员“{missingLocked}”本局不可用（被禁用），请解除锁定。", CandidateCount = candidates.Count };

        var allowed = _covenants.Select(c => c.Name)
            .Where(n => request.TransformAllowed.Contains(n) && !request.BannedCovenants.Contains(n) && !NoTransform.Contains(n))
            .ToList();
        // Transform targets that are not in covenant data (tests, custom data) still count.
        allowed.AddRange(request.TransformAllowed.Where(n => !allowed.Contains(n) && !request.BannedCovenants.Contains(n) && !NoTransform.Contains(n) && _covenants.All(c => c.Name != n)));

        var ctx = new SearchContext(request, candidates, allowed,
            [.. request.LockedOperators.Select(n => indexByName[n])], maxResultsPerVariant, maxNodes, cancellationToken);
        var variants = new List<PlannerVariantResult>();
        foreach (var transforms in request.TransformOptions)
            foreach (var people in request.PeopleOptions)
                variants.Add(ctx.SolveVariant(people, transforms));
        return new PlannerResult { Variants = variants, CandidateCount = candidates.Count };
    }

    /// <summary>
    /// Depth-first search over native covenant holders. Transforms are not branched on: once the remaining
    /// deficits can be covered by the transforms left, <see cref="TryFinish"/> assigns them to picked operators
    /// (adding a carrier operator only when nobody picked can take one).
    /// </summary>
    private sealed class SearchContext
    {
        private readonly PlannerRequest _request;
        private readonly List<Operator> _ops;
        private readonly HashSet<string>[] _native;
        private readonly bool[] _harmony;
        private readonly List<string> _allowedOrdered;
        private readonly HashSet<string> _allowed;
        private readonly List<int> _locked;
        private readonly HashSet<int> _lockedSet;
        private readonly int _maxResults;
        private readonly int _maxNodes;
        private readonly CancellationToken _ct;
        private readonly Dictionary<string, int> _rarity;
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly HashSet<string> _goalNames;
        private readonly bool _hasSoloGoal;

        // per-variant state
        private int _people, _transforms, _nodes;
        private bool _limited;
        private List<PlannedLineup> _found = [];
        private readonly List<int> _picked = [];
        private readonly bool[] _used;
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private static readonly IReadOnlyDictionary<int, string> NoTransforms = new Dictionary<int, string>();

        public SearchContext(PlannerRequest request, List<Operator> ops, List<string> allowed,
            List<int> locked, int maxResults, int maxNodes, CancellationToken ct)
        {
            _request = request; _ops = ops; _allowedOrdered = allowed; _allowed = [.. allowed];
            _locked = locked; _lockedSet = [.. locked]; _maxResults = maxResults; _maxNodes = maxNodes; _ct = ct;
            _native = ops.Select(o => o.CoreCovenants.Concat(o.AdditionalCovenants).ToHashSet(StringComparer.Ordinal)).ToArray();
            _harmony = ops.Select(o => o.CoreCovenants.Contains(CovenantCalculator.HarmonyCovenant)).ToArray();
            _used = new bool[ops.Count];
            _rarity = request.Goals.ToDictionary(g => g.Name, g => _native.Count(n => n.Contains(g.Name)));
            _goalNames = request.Goals.Select(g => g.Name).ToHashSet(StringComparer.Ordinal);
            _hasSoloGoal = request.Goals.Any(g => g.Name == SoloCovenant);
        }

        public PlannerVariantResult SolveVariant(int people, int transforms)
        {
            _ct.ThrowIfCancellationRequested();
            _people = people; _transforms = transforms; _nodes = 0; _limited = false; _found = [];
            _picked.Clear(); _visited.Clear(); Array.Clear(_used);
            if (_locked.Count > people) return new PlannerVariantResult(people, transforms, [], false);
            foreach (var i in _locked) { _picked.Add(i); _used[i] = true; }
            Visit();
            var limited = _limited;
            return new PlannerVariantResult(people, transforms,
                _found.Select(f => f with { SearchLimited = limited }).ToList(), limited);
        }

        private (Dictionary<string, int> Counts, Dictionary<string, int> Bonus) Count(IReadOnlyList<int> team, IReadOnlyDictionary<int, string> tr)
        {
            var counts = _request.Goals.ToDictionary(g => g.Name, _ => 0);
            var present = new HashSet<string>(StringComparer.Ordinal);
            var harmony = 0;
            foreach (var i in team)
            {
                var op = _ops[i];
                foreach (var c in op.CoreCovenants)
                {
                    if (c == CovenantCalculator.HarmonyCovenant) continue;
                    if (counts.ContainsKey(c)) counts[c]++;
                    present.Add(c);
                }
                foreach (var c in op.AdditionalCovenants)
                    if (counts.ContainsKey(c)) counts[c]++;
                if (_harmony[i])
                {
                    harmony++;
                    if (counts.ContainsKey(CovenantCalculator.HarmonyCovenant)) counts[CovenantCalculator.HarmonyCovenant]++;
                }
                if (tr.TryGetValue(i, out var t) && counts.ContainsKey(t)) counts[t]++;
            }
            var bonus = new Dictionary<string, int>(StringComparer.Ordinal);
            if (harmony > 0)
                foreach (var g in _request.Goals.Where(g => g.IsCore && present.Contains(g.Name)))
                {
                    counts[g.Name] += harmony;
                    bonus[g.Name] = harmony;
                }
            return (counts, bonus);
        }

        private bool Done => _found.Count >= _maxResults || _nodes > _maxNodes;

        private bool Contributes(int i, PlannerGoal g, Dictionary<string, int> counts)
            => _native[i].Contains(g.Name) || (g.IsCore && _harmony[i] && counts[g.Name] > 0);

        private void Visit()
        {
            if (_found.Count >= _maxResults) return;
            if (++_nodes > _maxNodes) { _limited = true; return; }
            if ((_nodes & 63) == 0) _ct.ThrowIfCancellationRequested();

            var (counts, _) = Count(_picked, NoTransforms);
            if (_hasSoloGoal && counts[SoloCovenant] > 1) return;
            var missing = _request.Goals.Where(g => counts[g.Name] < g.Count).ToList();
            var slots = _people - _picked.Count;
            var trLeft = _transforms;

            // Remaining deficits fit into the transforms: try to finish here.
            var transformDeficit = missing.Sum(g => g.Count - counts[g.Name]);
            if (missing.All(g => _allowed.Contains(g.Name)) && transformDeficit <= trLeft)
            {
                if (TryFinish(counts)) return;
                if (missing.Count == 0) return;
            }
            if (slots <= 0 || !CanStillReach(missing, counts, slots, trLeft)) return;
            if (!_visited.Add(StateKey())) return;

            // Branch on the hardest goal: ones that cannot be transformed first, then the rarest.
            var pivot = missing
                .OrderBy(g => _allowed.Contains(g.Name) ? 1 : 0)
                .ThenBy(g => _rarity[g.Name])
                .ThenByDescending(g => g.Count - counts[g.Name])
                .First();

            var branches = new List<(int Index, double Score)>();
            for (var i = 0; i < _ops.Count; i++)
            {
                if (_used[i] || !Contributes(i, pivot, counts)) continue;
                var gain = missing.Count(g => Contributes(i, g, counts));
                branches.Add((i, 10 * gain + (i * 13 % 31) / 50.0));
            }

            var expanded = 0;
            foreach (var b in branches.OrderByDescending(b => b.Score).ThenBy(b => b.Index))
            {
                if (Done || expanded++ >= MaxBranches) break;
                _picked.Add(b.Index); _used[b.Index] = true;
                Visit();
                _picked.RemoveAt(_picked.Count - 1); _used[b.Index] = false;
            }
        }

        /// <summary>Optimistic bound: can the remaining slots and transforms still cover every deficit?</summary>
        private bool CanStillReach(List<PlannerGoal> missing, Dictionary<string, int> counts, int slots, int trLeft)
        {
            var maxCover = 0;
            var unusedNative = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _ops.Count; i++)
            {
                if (_used[i]) continue;
                var cover = 0;
                foreach (var g in missing)
                {
                    if (!_native[i].Contains(g.Name) && !(g.IsCore && _harmony[i])) continue;
                    cover++;
                    unusedNative[g.Name] = unusedNative.GetValueOrDefault(g.Name) + 1;
                }
                if (cover > maxCover) maxCover = cover;
            }
            var totalDeficit = 0;
            foreach (var g in missing)
            {
                var deficit = g.Count - counts[g.Name];
                totalDeficit += deficit;
                var reach = Math.Min(slots, unusedNative.GetValueOrDefault(g.Name));
                if (_allowed.Contains(g.Name)) reach += trLeft;
                if (deficit > reach) return false;
            }
            return totalDeficit <= slots * maxCover + trLeft;
        }

        private string StateKey()
        {
            var ids = _picked.ToArray();
            Array.Sort(ids);
            return string.Join(',', ids);
        }

        /// <summary>Assigns transforms (and carrier operators if needed); records the lineup when every target is met.</summary>
        private bool TryFinish(Dictionary<string, int> nativeCounts)
        {
            var team = new List<int>(_picked);
            var tr = new Dictionary<int, string>();
            var usedHere = new HashSet<int>(team);

            int? FindHolder(string covenant)
            {
                foreach (var i in team.OrderBy(i => _lockedSet.Contains(i)))
                    if (!tr.ContainsKey(i) && !_native[i].Contains(covenant)) return i;
                if (team.Count >= _people) return null;
                // A carrier: an extra operator who takes the transform. Prefer ones that also hold target covenants.
                var carrier = Enumerable.Range(0, _ops.Count)
                    .Where(i => !_used[i] && !usedHere.Contains(i) && !_native[i].Contains(covenant))
                    .Where(i => !(_hasSoloGoal && _native[i].Contains(SoloCovenant)))
                    .OrderByDescending(i => _native[i].Count(_goalNames.Contains))
                    .ThenBy(i => _ops[i].Tier)
                    .Cast<int?>()
                    .FirstOrDefault();
                if (carrier is int c) { team.Add(c); usedHere.Add(c); }
                return carrier;
            }

            foreach (var g in _request.Goals)
            {
                var deficit = g.Count - nativeCounts[g.Name];
                for (var k = 0; k < deficit; k++)
                {
                    if (tr.Count >= _transforms || !_allowed.Contains(g.Name)) return false;
                    if (FindHolder(g.Name) is not int holder) return false;
                    tr[holder] = g.Name;
                }
            }

            var forced = 0;
            var goalTargets = _request.Goals.Select(g => g.Name).Where(_allowed.Contains).ToList();
            foreach (var i in team.OrderBy(i => _lockedSet.Contains(i)).ToList())
            {
                if (tr.Count >= _transforms) break;
                if (tr.ContainsKey(i)) continue;
                var target = goalTargets.FirstOrDefault(n => !_native[i].Contains(n))
                             ?? _allowedOrdered.FirstOrDefault(n => !_native[i].Contains(n));
                if (target is null) continue;
                tr[i] = target; forced++;
            }
            if (tr.Count != _transforms) return false;

            var (counts, bonus) = Count(team, tr);
            if (_request.Goals.Any(g => counts[g.Name] < g.Count)) return false;
            if (_hasSoloGoal && counts[SoloCovenant] != 1) return false;

            var key = string.Join(',', team.Select(i => _ops[i].Name).Order(StringComparer.Ordinal)) + '|' +
                      string.Join(',', tr.Select(x => _ops[x.Key].Name + ':' + x.Value).Order(StringComparer.Ordinal));
            if (!_seen.Add(key)) return true;

            var operators = team
                .OrderBy(i => _lockedSet.Contains(i) ? 0 : 1)
                .Select(i => new PlannedOperator(_ops[i].Name, tr.GetValueOrDefault(i), [.. _native[i]])
                {
                    IsLocked = _lockedSet.Contains(i),
                    IsHarmony = _harmony[i],
                    IconPath = _ops[i].IconPath
                }).ToList();
            _found.Add(new PlannedLineup(_people, _transforms, operators, counts, false)
            {
                Goals = _request.Goals,
                ForcedTransforms = forced,
                HarmonyBonus = bonus
            });
            return true;
        }
    }
}
