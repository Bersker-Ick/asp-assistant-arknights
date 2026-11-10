using ASPAssistant.Core.Models;
using ASPAssistant.Core.Services;
using ASPAssistant.Core.ViewModels;
using FluentAssertions;

namespace ASPAssistant.Tests.Services;

public class PlannerCalculationIntegrationTests
{
    private static readonly Operator[] Operators =
    [
        new() { Name = "甲", CoreCovenants = ["乌萨斯"], AdditionalCovenants = ["精准"] },
        new() { Name = "乙", CoreCovenants = ["炎"] },
        new() { Name = "魔王", CoreCovenants = ["调和"] },
    ];
    private static readonly CovenantInfo[] Covenants =
    [
        new() { Name = "炎", BondType = "SEASON", ActivateCount = 3 },
        new() { Name = "乌萨斯", BondType = "SEASON", ActivateCount = 3 },
        new() { Name = "精准", BondType = "REGULAR", ActivateCount = 2 },
        new() { Name = "调和", BondType = "REGULAR", ActivateCount = 1 },
    ];
    private readonly CovenantCalculator _calculator = new(Operators, Covenants,
        new Dictionary<string, string> { ["炎转职"] = "炎" });

    [Fact]
    public void TransformedCoreReceivesHarmonyBonus()
    {
        var lineup = new Lineup { Slots =
        [
            new() { OperatorName = "甲", TransformedCovenant = "炎" },
            new() { OperatorName = "魔王" },
        ] };
        _calculator.Compute(lineup).Single(c => c.Name == "炎").Count.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeOrEquipmentCovenantIsNotCountedAgainByPlannerMetadata(bool fromEquipment)
    {
        var lineup = new Lineup { Slots = [new()
        {
            OperatorName = fromEquipment ? "甲" : "乙",
            TransformedCovenant = "炎",
            Equipments = fromEquipment ? [CovenantCalculator.ShapeshifterEquipment, "炎转职"] : [],
        }] };
        _calculator.Compute(lineup).Single(c => c.Name == "炎").Count.Should().Be(1);
    }

    [Fact]
    public void NativeAdditionalCovenantIsNotCountedAgainByPlannerMetadata()
    {
        var lineup = new Lineup { Slots = [new() { OperatorName = "甲", TransformedCovenant = "精准" }] };
        _calculator.Compute(lineup).Single(c => c.Name == "精准").Count.Should().Be(1);
    }

    [Fact]
    public void PlannerAndSavedLineupAgreeWhenTransformationIntroducesCoreForHarmony()
    {
        var planner = new LineupPlanner(Operators, Covenants);
        var result = planner.Solve(new PlannerRequest
        {
            Goals = [new("炎", 1, 1, false, true)],
            LockedOperators = new HashSet<string> { "甲", "魔王" },
            TransformAllowed = new HashSet<string> { "炎" },
            PeopleOptions = [2], TransformOptions = [1],
        }).Should().ContainSingle().Subject;
        result.Counts["炎"].Should().Be(2);
        var saved = new PlannerLineupItem { Source = result }.ToLineup();
        _calculator.Compute(saved).Single(c => c.Name == "炎").Count.Should().Be(result.Counts["炎"]);
        var editor = new LineupEditorViewModel(saved, Operators, [], _calculator);
        editor.ToLineup().Slots.Single(s => s.OperatorName == "甲").TransformedCovenant.Should().Be("炎");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HarmonyAndTwoTransformsCanReachThreeCoreMembersWithoutNativeHolder(bool locked)
    {
        Operator[] operators = [Operators[0], new() { Name = "丙", CoreCovenants = ["乌萨斯"] }, Operators[2]];
        var planner = new LineupPlanner(operators, Covenants);
        var results = planner.Solve(new PlannerRequest
        {
            Goals = [new("炎", 1, 3, false, true)],
            LockedOperators = locked ? new HashSet<string> { "甲", "丙", "魔王" } : new HashSet<string>(),
            TransformAllowed = new HashSet<string> { "炎" },
            PeopleOptions = [8], TransformOptions = [2],
        });
        results.Should().NotBeEmpty();
        var calculator = new CovenantCalculator(operators, Covenants, new Dictionary<string, string>());
        foreach (var result in results)
        {
            result.Counts["炎"].Should().BeGreaterThanOrEqualTo(3);
            calculator.Compute(new PlannerLineupItem { Source = result }.ToLineup())
                .Single(c => c.Name == "炎").Count.Should().Be(result.Counts["炎"]);
        }
    }
}
