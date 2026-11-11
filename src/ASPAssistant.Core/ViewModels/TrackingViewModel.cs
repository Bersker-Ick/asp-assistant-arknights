// Modified for ASP Assistant Regulation on 2026-11-09; see NOTICE.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ASPAssistant.Core.Models;

namespace ASPAssistant.Core.ViewModels;

public partial class TrackingViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _trackMultipleLineups;

    [ObservableProperty]
    private ObservableCollection<TrackingEntry> _trackedOperators = [];

    [ObservableProperty]
    private ObservableCollection<TrackingEntry> _trackedEquipment = [];

    [ObservableProperty]
    private Dictionary<string, int> _covenantCounts = new();

    [ObservableProperty]
    private object? _gameState;

    /// <summary>The saved lineup currently feeding the tracking list, shown by the phase switch.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceLineup), nameof(SourceLineupName), nameof(SourcePhaseOperatorCount))]
    private Lineup? _sourceLineup;

    /// <summary>Which phase of <see cref="SourceLineup"/> is currently tracked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourcePhaseOperatorCount))]
    private LineupPhase _sourcePhase = LineupPhase.后期;

    /// <summary>Names this lineup added to the tracking list; switching phases swaps only these.</summary>
    private readonly HashSet<string> _sourceTrackedNames = new(StringComparer.Ordinal);

    /// <summary>Whether the remembered lineup contributes equipment as well as operators.</summary>
    private bool _sourceTracksEquipment = true;

    public bool HasSourceLineup => SourceLineup is not null;

    public string SourceLineupName => SourceLineup is null ? ""
        : string.IsNullOrWhiteSpace(SourceLineup.Name) ? "未命名阵容" : SourceLineup.Name;

    public int SourcePhaseOperatorCount => SourceLineup is null
        ? 0
        : PhaseSlots(SourceLineup, SourcePhase).Count(s => !string.IsNullOrEmpty(s.OperatorName));

    public IReadOnlyList<TrackingEntry> AllEntries =>
        [.. TrackedOperators, .. TrackedEquipment];

    public (int Operators, int Equipment) TrackLineup(Lineup lineup)
    {
        if (!TrackMultipleLineups) ClearAllTracking();
        else ClearSourceLineup();
        var previousOperators = TrackedOperators.Count;
        var previousEquipment = TrackedEquipment.Count;
        foreach (var slot in lineup.Slots)
        {
            TrackSlotOperator(slot);
            foreach (var equipment in slot.Equipments)
                if (!string.IsNullOrEmpty(equipment))
                    AddTracking(equipment, TrackingType.Equipment);
        }
        return (TrackedOperators.Count - previousOperators, TrackedEquipment.Count - previousEquipment);
    }

    /// <summary>
    /// Tracks one saved lineup at the requested phase (late by default) and remembers it as the
    /// source of the tracking page's phase switch. Re-tracking the same lineup keeps the phase
    /// the player already chose. <paramref name="includeEquipment"/> lets the planner board keep
    /// its operators-only batch semantics while still enabling the phase switch.
    /// </summary>
    public (int Operators, int Equipment) TrackSavedLineup(
        Lineup lineup, LineupPhase? phase = null, bool includeEquipment = true)
    {
        var sameLineup = SourceLineup?.Id == lineup.Id;
        var targetPhase = phase ?? (sameLineup ? SourcePhase : LineupPhase.后期);
        if (!TrackMultipleLineups)
        {
            ClearTrackingEntries();
            _sourceTrackedNames.Clear();
        }
        else if (!sameLineup)
        {
            _sourceTrackedNames.Clear();
        }

        _sourceTracksEquipment = includeEquipment;
        SourceLineup = lineup;
        SourcePhase = targetPhase;
        var previousOperators = TrackedOperators.Count;
        var previousEquipment = TrackedEquipment.Count;
        ApplySourcePhase(removeOwned: sameLineup);
        return (TrackedOperators.Count - previousOperators, TrackedEquipment.Count - previousEquipment);
    }

    /// <summary>Switches the tracked phase of the remembered saved lineup, swapping only its entries.</summary>
    public (int Operators, int Equipment) SelectTrackedLineupPhase(LineupPhase phase)
    {
        if (SourceLineup is null) return (0, 0);

        if (!TrackMultipleLineups)
        {
            ClearTrackingEntries();
            _sourceTrackedNames.Clear();
        }
        var previousOperators = TrackedOperators.Count;
        var previousEquipment = TrackedEquipment.Count;
        SourcePhase = phase;
        ApplySourcePhase(removeOwned: true);
        return (TrackedOperators.Count - previousOperators, TrackedEquipment.Count - previousEquipment);
    }

    /// <summary>
    /// Adds every operator of the given lineups in one batch, merging duplicates across teams.
    /// Equipment is intentionally left out: the planner's saved-lineup board tracks late-phase operators only.
    /// Returns the number of operators added to the tracking list.
    /// </summary>
    public int TrackLineupsOperators(IEnumerable<Lineup> lineups)
    {
        var batch = lineups as IReadOnlyCollection<Lineup> ?? lineups.ToList();
        if (!TrackMultipleLineups) ClearAllTracking();
        else ClearSourceLineup();
        var previous = TrackedOperators.Count;
        foreach (var lineup in batch)
            foreach (var slot in lineup.Slots)
                TrackSlotOperator(slot);
        return TrackedOperators.Count - previous;
    }

    /// <summary>Adds the slot's operator, returns true only when it was newly added.</summary>
    private bool TrackSlotOperator(LineupSlot slot)
    {
        if (string.IsNullOrEmpty(slot.OperatorName)) return false;
        var existing = TrackedOperators.FirstOrDefault(e => e.Name == slot.OperatorName);
        var entry = new TrackingEntry
        {
            Name = slot.OperatorName, Type = TrackingType.Operator,
            TransformedCovenant = slot.TransformedCovenant
        };
        if (existing is null)
        {
            TrackedOperators.Add(entry);
            return true;
        }
        if (existing.TransformedCovenant != slot.TransformedCovenant)
            TrackedOperators[TrackedOperators.IndexOf(existing)] = entry;
        return false;
    }

    private void ApplySourcePhase(bool removeOwned)
    {
        if (SourceLineup is not { } lineup) return;
        var slots = PhaseSlots(lineup, SourcePhase);
        var phaseNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.OperatorName)) phaseNames.Add(slot.OperatorName);
            if (!_sourceTracksEquipment) continue;
            foreach (var equipment in slot.Equipments)
                if (!string.IsNullOrEmpty(equipment)) phaseNames.Add(equipment);
        }

        if (removeOwned)
        {
            foreach (var name in _sourceTrackedNames.Where(n => !phaseNames.Contains(n)).ToList())
            {
                RemoveTracking(name);
                _sourceTrackedNames.Remove(name);
            }
        }

        foreach (var slot in slots)
        {
            if (TrackSlotOperator(slot)) _sourceTrackedNames.Add(slot.OperatorName);
            if (!_sourceTracksEquipment) continue;
            foreach (var equipment in slot.Equipments)
            {
                if (string.IsNullOrEmpty(equipment) || IsTracked(equipment)) continue;
                AddTracking(equipment, TrackingType.Equipment);
                _sourceTrackedNames.Add(equipment);
            }
        }
    }

    private static IReadOnlyList<LineupSlot> PhaseSlots(Lineup lineup, LineupPhase phase) => phase switch
    {
        LineupPhase.前期 => lineup.EarlySlots,
        LineupPhase.中期 => lineup.MidSlots,
        LineupPhase.挂件 => lineup.SupportSlots,
        _ => lineup.Slots,
    };

    public void AddTracking(string name, TrackingType type)
    {
        if (IsTracked(name))
            return;

        var entry = new TrackingEntry { Name = name, Type = type };
        if (type == TrackingType.Operator)
            TrackedOperators.Add(entry);
        else
            TrackedEquipment.Add(entry);
    }

    public void RemoveTracking(string name)
    {
        _sourceTrackedNames.Remove(name);
        var opEntry = TrackedOperators.FirstOrDefault(e => e.Name == name);
        if (opEntry != null)
        {
            TrackedOperators.Remove(opEntry);
            return;
        }

        var eqEntry = TrackedEquipment.FirstOrDefault(e => e.Name == name);
        if (eqEntry != null)
            TrackedEquipment.Remove(eqEntry);
    }

    public void ClearAllTracking()
    {
        ClearTrackingEntries();
        ClearSourceLineup();
    }

    private void ClearTrackingEntries()
    {
        TrackedOperators.Clear();
        TrackedEquipment.Clear();
    }

    private void ClearSourceLineup()
    {
        SourceLineup = null;
        SourcePhase = LineupPhase.后期;
        _sourceTrackedNames.Clear();
        _sourceTracksEquipment = true;
    }

    public void ClearPlannedTransformation(string name)
    {
        var entry = TrackedOperators.Single(e => e.Name == name);
        TrackedOperators[TrackedOperators.IndexOf(entry)] = new TrackingEntry { Name = name, Type = TrackingType.Operator };
    }

    public bool IsTracked(string name)
    {
        return TrackedOperators.Any(e => e.Name == name)
            || TrackedEquipment.Any(e => e.Name == name);
    }

    public void LoadEntries(List<TrackingEntry> entries)
    {
        ClearTrackingEntries();
        ClearSourceLineup();
        foreach (var entry in entries)
        {
            if (entry.Type == TrackingType.Operator)
                TrackedOperators.Add(entry);
            else
                TrackedEquipment.Add(entry);
        }
    }
}
