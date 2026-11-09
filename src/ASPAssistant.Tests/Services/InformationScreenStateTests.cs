using ASPAssistant.Core.Services;
using FluentAssertions;

namespace ASPAssistant.Tests.Services;

public class InformationScreenStateTests
{
    [Fact]
    public void OcrMissesNeverRearmASuccessfullyEnteredOpening()
    {
        var state = new InformationScreenState();
        state.Observe(true, false).Should().BeFalse();
        state.Observe(true, false).Should().BeTrue();
        for (int i = 0; i < 100; i++) state.Observe(false, false).Should().BeFalse();
        state.Observe(true, false).Should().BeFalse();
        state.Observe(true, false).Should().BeFalse();
        state.IsConsumed.Should().BeTrue();
    }

    [Fact]
    public void MinimizingResizingOrLosingFocusSuspendsWithoutForgettingRecording()
    {
        var state = new InformationScreenState();
        state.MarkRecorded();
        state.Observe(false, true);
        state.Suspend();
        state.Observe(false, true);
        state.IsConsumed.Should().BeTrue("a frame interrupted by resizing cannot confirm gameplay");
        state.Suspend();
        state.Observe(true, false).Should().BeFalse();
        state.Observe(true, false).Should().BeFalse();
    }

    [Fact]
    public void OnlyConfirmedGameplayAllowsTheNextOpening()
    {
        var state = new InformationScreenState();
        state.MarkRecorded();
        state.Observe(false, true);
        state.Observe(false, true);
        state.IsConsumed.Should().BeFalse();
        state.Observe(false, false).Should().BeFalse("an in-game info panel has no opening title");
        state.Observe(true, false).Should().BeFalse();
        state.Observe(true, false).Should().BeTrue();
        state.Observe(true, false).Should().BeFalse();
    }

    [Fact]
    public void ManualRecordingConsumesThePendingAutomaticEntry()
    {
        var state = new InformationScreenState();
        state.Observe(true, false);
        state.MarkRecorded();
        state.Observe(true, false).Should().BeFalse();
    }
}
