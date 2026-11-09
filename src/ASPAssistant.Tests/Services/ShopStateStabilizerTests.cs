using ASPAssistant.Core.Models;
using ASPAssistant.Core.Services;
using FluentAssertions;

namespace ASPAssistant.Tests.Services;

public class ShopStateStabilizerTests
{
    [Fact]
    public void MissedFrameIsTolerated_ThenClosedShopClears()
    {
        var stabilizer = new ShopStateStabilizer();
        stabilizer.TryUpdate([new() { Id = "奥斯塔@0", Name = "奥斯塔" }], out _).Should().BeTrue();
        stabilizer.TryUpdate([], out var grace).Should().BeFalse();
        grace.Should().ContainSingle();
        stabilizer.TryUpdate([], out var cleared).Should().BeTrue();
        cleared.Should().BeEmpty();
    }

    [Fact]
    public void ResizeUpdatesExistingMarker_WithoutCreatingAnotherInstance()
    {
        var stabilizer = new ShopStateStabilizer();
        stabilizer.TryUpdate([new() { Id = "奥斯塔@0", OcrRegion = (.4, .93, .04, .02) }], out _);
        stabilizer.TryUpdate([new() { Id = "奥斯塔@0", OcrRegion = (.5, .93, .04, .02) }], out var moved)
            .Should().BeTrue();
        moved.Should().ContainSingle().Which.OcrRegion.X.Should().Be(.5);
        stabilizer.TryUpdate(moved, out _).Should().BeFalse();
    }

    [Fact]
    public void SuccessfulReadResetsMissCount()
    {
        var stabilizer = new ShopStateStabilizer();
        ShopItem[] items = [new() { Id = "突袭手雷@0", Name = "突袭手雷" }];
        stabilizer.TryUpdate(items, out _);
        stabilizer.TryUpdate([], out _);
        stabilizer.TryUpdate(items, out _);
        stabilizer.TryUpdate([], out var kept).Should().BeFalse();
        kept.Should().ContainSingle();
    }
}
