// Modified for ASP Assistant Regulation on 2026-11-09; see NOTICE.
using ASPAssistant.Core.Models;
using ASPAssistant.Core.ViewModels;
using FluentAssertions;

namespace ASPAssistant.Tests.ViewModels;

public class TrackingViewModelTests
{
    [Fact]
    public void MergedLineupsUseLatestTransformationAndNotifySoChangesAreSaved()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        var changes = 0;
        vm.TrackedOperators.CollectionChanged += (_, _) => changes++;
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "角峰", TransformedCovenant = "炎" }] });
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "角峰", TransformedCovenant = "维多利亚" }] });
        vm.TrackedOperators.Should().ContainSingle().Which.TransformedCovenant.Should().Be("维多利亚");
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "角峰" }] });
        vm.TrackedOperators.Single().TransformedCovenant.Should().BeEmpty();
        changes.Should().Be(3);
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "角峰", TransformedCovenant = "炎" }] });
        vm.ClearPlannedTransformation("角峰");
        changes.Should().Be(5);
        vm.TrackedOperators.Single().TransformedCovenant.Should().BeEmpty();
    }

    [Fact]
    public void AddTracking_AddsEntry()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("角峰", TrackingType.Operator);
        vm.TrackedOperators.Should().ContainSingle(e => e.Name == "角峰");
    }

    [Fact]
    public void AddTracking_Duplicate_DoesNotAdd()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("角峰", TrackingType.Operator);
        vm.AddTracking("角峰", TrackingType.Operator);
        vm.TrackedOperators.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveTracking_RemovesEntry()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("角峰", TrackingType.Operator);
        vm.RemoveTracking("角峰");
        vm.TrackedOperators.Should().BeEmpty();
    }

    [Fact]
    public void IsTracked_ReturnsTrueForTrackedItem()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("角峰", TrackingType.Operator);
        vm.IsTracked("角峰").Should().BeTrue();
        vm.IsTracked("隐现").Should().BeFalse();
    }

    [Fact]
    public void AllEntries_ReturnsBothTypes()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("角峰", TrackingType.Operator);
        vm.AddTracking("不屈弹射器", TrackingType.Equipment);
        vm.AllEntries.Should().HaveCount(2);
        vm.TrackedOperators.Should().HaveCount(1);
        vm.TrackedEquipment.Should().HaveCount(1);
    }

    [Fact]
    public void TrackLineup_DefaultModeReplacesExistingTargets()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("隐现", TrackingType.Operator);
        vm.AddTracking("旧装备", TrackingType.Equipment);

        var added = vm.TrackLineup(new Lineup
        {
            Slots = [new() { OperatorName = "角峰", Equipments = ["不屈弹射器"] }]
        });

        vm.TrackMultipleLineups.Should().BeFalse();
        vm.TrackedOperators.Should().ContainSingle(e => e.Name == "角峰");
        vm.TrackedEquipment.Should().ContainSingle(e => e.Name == "不屈弹射器");
        added.Should().Be((1, 1));
    }

    [Fact]
    public void TrackLineup_MultipleModeMergesAndDeduplicatesTeams()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        vm.TrackLineup(new Lineup
        {
            Slots = [new() { OperatorName = "角峰", Equipments = ["不屈弹射器"] }]
        });
        var added = vm.TrackLineup(new Lineup
        {
            Slots =
            [
                new() { OperatorName = "角峰", Equipments = ["不屈弹射器"] },
                new() { OperatorName = "隐现", Equipments = ["乌萨斯呼吸器", "不屈弹射器"] }
            ]
        });

        vm.TrackedOperators.Select(e => e.Name).Should().BeEquivalentTo(["角峰", "隐现"]);
        vm.TrackedEquipment.Select(e => e.Name).Should().BeEquivalentTo(["不屈弹射器", "乌萨斯呼吸器"]);
        added.Should().Be((1, 1));
    }

    [Fact]
    public void TrackLineup_DisablingMultipleModeKeepsListUntilNextTeam()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "角峰" }] });
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "隐现" }] });

        vm.TrackMultipleLineups = false;
        vm.TrackedOperators.Should().HaveCount(2);
        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "惊蛰" }] });

        vm.TrackedOperators.Should().ContainSingle(e => e.Name == "惊蛰");
    }

    [Fact]
    public void TrackLineup_SameTeamTwiceDoesNotDuplicateTargets()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        var lineup = new Lineup
        {
            Slots = [new() { OperatorName = "角峰", Equipments = ["不屈弹射器"] }]
        };
        vm.TrackLineup(lineup);

        vm.TrackLineup(lineup).Should().Be((0, 0));
        vm.AllEntries.Should().HaveCount(2);
    }

    [Fact]
    public void TrackLineupsOperatorsMergesTeamsAndLeavesEquipmentAlone()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        var added = vm.TrackLineupsOperators(
        [
            new Lineup { Slots = [new() { OperatorName = "角峰", Equipments = ["不屈弹射器"] }] },
            new Lineup
            {
                Slots =
                [
                    new() { OperatorName = "角峰", TransformedCovenant = "炎" },
                    new() { OperatorName = "隐现", TransformedCovenant = "维多利亚" }
                ]
            },
        ]);

        added.Should().Be(2);
        vm.TrackedOperators.Select(e => e.Name).Should().Equal("角峰", "隐现");
        vm.TrackedOperators.Single(e => e.Name == "角峰").TransformedCovenant.Should().Be("炎",
            "the latest planned transformation wins");
        vm.TrackedEquipment.Should().BeEmpty("the saved-lineup board tracks operators only");
        vm.TrackLineupsOperators([]).Should().Be(0);
    }

    [Fact]
    public void TrackLineupsOperatorsDefaultModeReplacesExistingTargets()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("旧干员", TrackingType.Operator);
        vm.AddTracking("旧装备", TrackingType.Equipment);

        var added = vm.TrackLineupsOperators(
        [
            new Lineup { Slots = [new() { OperatorName = "角峰" }] },
            new Lineup { Slots = [new() { OperatorName = "隐现" }] },
        ]);

        vm.TrackMultipleLineups.Should().BeFalse();
        added.Should().Be(2);
        vm.TrackedOperators.Select(e => e.Name).Should().Equal("角峰", "隐现");
        vm.TrackedEquipment.Should().BeEmpty();
    }

    [Fact]
    public void TrackSavedLineupPhaseSwitchSwapsOnlyThatLineupsEntries()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        vm.AddTracking("常驻干员", TrackingType.Operator);
        var lineup = new Lineup
        {
            Name = "阶段阵容",
            EarlySlots = [new() { OperatorName = "前期干员", Equipments = ["前期装备"] }],
            MidSlots = [new() { OperatorName = "中期干员", Equipments = ["中期装备"] }],
            Slots = [new() { OperatorName = "后期干员", Equipments = ["后期装备"] }],
        };

        vm.TrackSavedLineup(lineup).Should().Be((1, 1));
        vm.HasSourceLineup.Should().BeTrue();
        vm.SourceLineupName.Should().Be("阶段阵容");
        vm.SourcePhase.Should().Be(LineupPhase.后期);
        vm.SourcePhaseOperatorCount.Should().Be(1);
        vm.TrackedOperators.Select(e => e.Name).Should().Contain(["常驻干员", "后期干员"]);
        vm.TrackedEquipment.Select(e => e.Name).Should().Contain("后期装备");

        vm.SelectTrackedLineupPhase(LineupPhase.前期).Should().Be((0, 0));
        vm.SourcePhase.Should().Be(LineupPhase.前期);
        vm.SourcePhaseOperatorCount.Should().Be(1);
        vm.TrackedOperators.Select(e => e.Name).Should().Contain(["常驻干员", "前期干员"]);
        vm.TrackedOperators.Select(e => e.Name).Should().NotContain("后期干员");
        vm.TrackedEquipment.Select(e => e.Name).Should().Contain("前期装备");
        vm.TrackedEquipment.Select(e => e.Name).Should().NotContain("后期装备");

        vm.TrackSavedLineup(lineup);
        vm.SourcePhase.Should().Be(LineupPhase.前期, "re-tracking the same lineup keeps the chosen phase");
    }

    [Fact]
    public void PhaseSwitchKeepsSharedOperatorsAndOtherTrackingResetsTheSource()
    {
        var vm = new TrackingViewModel();
        vm.AddTracking("旧目标", TrackingType.Operator);
        var lineup = new Lineup
        {
            EarlySlots = [new() { OperatorName = "角峰" }, new() { OperatorName = "隐现" }],
            Slots = [new() { OperatorName = "角峰" }],
        };

        vm.TrackSavedLineup(lineup);
        vm.TrackedOperators.Select(e => e.Name).Should().Equal("角峰");
        vm.SelectTrackedLineupPhase(LineupPhase.前期);
        vm.TrackedOperators.Select(e => e.Name).Should().BeEquivalentTo("角峰", "隐现");
        vm.SelectTrackedLineupPhase(LineupPhase.后期);
        vm.TrackedOperators.Select(e => e.Name).Should().Equal("角峰");

        vm.TrackLineup(new Lineup { Slots = [new() { OperatorName = "其他队伍" }] });
        vm.HasSourceLineup.Should().BeFalse("tracking a planner result clears the saved-lineup source");
        vm.TrackedOperators.Select(e => e.Name).Should().Equal("其他队伍");

        vm.TrackSavedLineup(lineup);
        vm.ClearAllTracking();
        vm.HasSourceLineup.Should().BeFalse();
        vm.AllEntries.Should().BeEmpty();
    }

    [Fact]
    public void TrackSavedLineupCanStayOperatorsOnlyAndStillSwitchPhases()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        var lineup = new Lineup
        {
            Name = "只看干员",
            EarlySlots = [new() { OperatorName = "前期干员", Equipments = ["前期装备"] }],
            Slots = [new() { OperatorName = "后期干员", Equipments = ["后期装备"] }],
        };

        vm.TrackSavedLineup(lineup, includeEquipment: false);
        vm.TrackedOperators.Select(e => e.Name).Should().Contain("后期干员");
        vm.TrackedEquipment.Should().BeEmpty();

        vm.SelectTrackedLineupPhase(LineupPhase.前期);
        vm.TrackedOperators.Select(e => e.Name).Should().Contain("前期干员");
        vm.TrackedEquipment.Should().BeEmpty("the board tracks operators only, even across phases");
    }

    [Fact]
    public void ClearAllTracking_RemovesBothTypesAndKeepsMultipleMode()
    {
        var vm = new TrackingViewModel { TrackMultipleLineups = true };
        vm.TrackLineup(new Lineup
        {
            Slots = [new() { OperatorName = "角峰", Equipments = ["不屈弹射器"] }]
        });

        vm.ClearAllTracking();

        vm.AllEntries.Should().BeEmpty();
        vm.TrackMultipleLineups.Should().BeTrue();
    }
}
