using ASPAssistant.Core.Models;
using ASPAssistant.Core.ViewModels;
using FluentAssertions;

namespace ASPAssistant.Tests.ViewModels;

public class EnemyViewModelTests
{
    [Fact]
    public void BusyStatesDisableRetryUntilBothScansFinish()
    {
        var vm = new EnemyViewModel();
        vm.BeginRecognition();
        vm.CanRecognize.Should().BeFalse();
        vm.IsBanAnalysisRunning = true;
        vm.RecognitionFailed("未获取到游戏画面");
        vm.CanRecognize.Should().BeFalse();
        vm.IsBanAnalysisRunning = false;
        vm.CanRecognize.Should().BeTrue();
        vm.RecognitionStatus.Should().Be("未获取到游戏画面");
    }

    [Fact]
    public void UnknownRecordsStayVisible_AndAreReportedAsPartial()
    {
        var vm = new EnemyViewModel();
        vm.Update(new("假想敌：鳍", ["特训敌人·元素", "特训敌人·致命"], ["假想敌：鳍", "特训敌人·致命"]),
            null, [new() { TypeName = "元素" }, null]);
        vm.BossName.Should().Be("假想敌：鳍");
        vm.EnemyTypes.Should().HaveCount(2);
        vm.EnemyTypeDetails.Should().ContainSingle().Which.TypeName.Should().Be("元素");
        vm.UnmatchedEntries.Should().HaveCount(2);
        vm.UnmatchedEntries.Should().Contain(s => s.Contains("致命"));
        vm.RawTexts.Should().Contain("假想敌：鳍");
        vm.RecognitionStatus.Should().StartWith("部分识别");
    }

    [Fact]
    public void EmptyRetryPreservesPreviousEnemyDetails()
    {
        var vm = new EnemyViewModel();
        vm.Update(new("假想敌：胄", ["特训敌人·元素"], ["假想敌：胄"]),
            new() { Name = "假想敌：胄", Description = "旧资料", AbilityList = ["旧技能"] }, [new() { TypeName = "元素" }]);
        vm.RecognitionStatus.Should().StartWith("识别完成");
        vm.BeginRecognition();
        vm.Update(new("", [], []), null, []);
        vm.RecognitionStatus.Should().Contain("保留上次录入结果");
        vm.BossAbilities.Should().Equal("旧技能");
        vm.BossDescription.Should().Be("旧资料");
        vm.EnemyTypeDetails.Should().ContainSingle();
        vm.UnmatchedEntries.Should().BeEmpty();
        vm.CanRecognize.Should().BeTrue();
    }

    [Fact]
    public void CaptureFailureKeepsPreviousResultsAndAllowsRetry()
    {
        var vm = new EnemyViewModel();
        vm.Update(new("未知首领", ["特训敌人·复苏"], ["未知首领"]), null, [null]);
        vm.BeginRecognition();
        vm.RecognitionFailed("请恢复游戏窗口后重试");
        vm.BossName.Should().Be("未知首领");
        vm.RawTexts.Should().Contain("未知首领");
        vm.UnmatchedEntries.Should().HaveCount(2);
        vm.IsRecognizing.Should().BeFalse();
        vm.CanRecognize.Should().BeTrue();
    }

    [Fact]
    public void ValidRetryReplacesOldDetailsOnlyAfterCompletion()
    {
        var vm = new EnemyViewModel();
        vm.Update(new("旧首领", ["特训敌人·元素"], ["旧首领"]),
            new() { Name = "旧首领", AbilityList = ["旧技能"] }, [new() { TypeName = "元素" }]);
        vm.BeginRecognition();
        vm.BossName.Should().Be("旧首领");
        vm.RecognitionStatus.Should().Contain("上次录入结果");
        vm.Update(new("新首领", ["特训敌人·隐匿"], ["新首领"]),
            new() { Name = "新首领", AbilityList = ["新技能"] }, [new() { TypeName = "隐匿" }]);
        vm.BossName.Should().Be("新首领");
        vm.BossAbilities.Should().Equal("新技能");
        vm.EnemyTypeDetails.Should().ContainSingle().Which.TypeName.Should().Be("隐匿");
    }
}
