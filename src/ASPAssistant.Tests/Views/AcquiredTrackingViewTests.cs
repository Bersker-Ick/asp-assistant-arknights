using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ASPAssistant.App;
using ASPAssistant.App.Controls;
using ASPAssistant.App.Views;
using ASPAssistant.Core.GameState;
using ASPAssistant.Core.Models;
using ASPAssistant.Core.Services;
using ASPAssistant.Core.ViewModels;
using FluentAssertions;

namespace ASPAssistant.Tests.Views;

internal static class AcquiredTrackingViewTests
{
    public static void Verify()
    {
        const string operatorName = "名字很长的测试干员甲";
        const string equipmentName = "名字很长的测试防御装备";
        foreach (var light in new[] { false, true })
        {
            ThemeManager.Apply(light);
            Operator[] operators = [new() { Name = operatorName, Tier = 5 }];
            Equipment[] equipment = [new() { Name = equipmentName, Categories = ["防御", "生命"] }];
            var vm = new TrackingViewModel();
            vm.AddTracking(operatorName, TrackingType.Operator);
            vm.AddTracking(equipmentName, TrackingType.Equipment);
            vm.AddTracking("旧版已移除干员", TrackingType.Operator);
            vm.AddTracking("旧版已移除装备", TrackingType.Equipment);
            var view = new TrackingView();
            view.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            view.SetOperators(operators);
            view.SetEquipment(equipment);
            view.SetCovenantCalculator(new CovenantCalculator(operators, [], new Dictionary<string, string>()));
            view.ToggleAcquiredRequested += vm.ToggleAcquired;
            view.RemoveTrackingRequested += vm.RemoveTracking;
            view.DataContext = vm;
            Layout(view);

            var opCard = Descendants<OperatorCard>(view).Single();
            var eqCard = Descendants<TrackedEquipmentCard>(view).Single();
            foreach (var card in new UserControl[] { opCard, eqCard })
            {
                var acquire = (Button)card.FindName("AcquiredButton");
                var remove = (Button)card.FindName("TrackButton");
                acquire.IsEnabled.Should().BeTrue();
                acquire.Visibility.Should().Be(Visibility.Visible);
                acquire.TranslatePoint(new Point(acquire.ActualWidth, 0), card).X
                    .Should().BeLessThanOrEqualTo(remove.TranslatePoint(new Point(), card).X);
                remove.TranslatePoint(new Point(remove.ActualWidth, 0), view).X.Should().BeLessThanOrEqualTo(280);
            }
            ((TextBlock)eqCard.FindName("NameText")).ActualWidth.Should().BeGreaterThan(40);
            Save(view, light ? "light-before" : "dark-before");

            ((Button)opCard.FindName("AcquiredButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)eqCard.FindName("AcquiredButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(view);
            vm.TrackedOperators.Should().HaveCount(2);
            vm.TrackedEquipment.Should().HaveCount(2);
            var acquiredOp = Descendants<OperatorCard>(view).Single();
            var acquiredEq = Descendants<TrackedEquipmentCard>(view).Single();
            acquiredOp.IsAcquired.Should().BeTrue();
            acquiredOp.IsTracked.Should().BeFalse();
            acquiredEq.IsAcquired.Should().BeTrue();
            ((Button)acquiredOp.FindName("AcquiredButton")).IsEnabled.Should().BeTrue();
            ((Button)acquiredEq.FindName("AcquiredButton")).IsEnabled.Should().BeTrue();
            ((Button)acquiredOp.FindName("TrackButton")).IsEnabled.Should().BeTrue();
            Save(view, light ? "light-acquired" : "dark-acquired");
            for (var cycle = 0; cycle < 2; cycle++)
            {
                ((Button)Descendants<OperatorCard>(view).Single().FindName("AcquiredButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)Descendants<TrackedEquipmentCard>(view).Single().FindName("AcquiredButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(view);
                vm.IsTracked(operatorName).Should().BeTrue();
                vm.IsTracked(equipmentName).Should().BeTrue();
                vm.AllEntries.Should().HaveCount(4);
                ((Run)view.FindName("PendingOperatorCount")).Text.Should().Be("2");
                ((Run)view.FindName("PendingEquipmentCount")).Text.Should().Be("2");
                ((Button)Descendants<OperatorCard>(view).Single().FindName("AcquiredButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)Descendants<TrackedEquipmentCard>(view).Single().FindName("AcquiredButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(view);
                vm.IsTracked(operatorName).Should().BeFalse();
                vm.IsTracked(equipmentName).Should().BeFalse();
            }


            foreach (var name in new[] { "旧版已移除干员", "旧版已移除装备" })
            {
                Descendants<Button>(view).Single(b => b.Name == "AcquireUnknownButton"
                    && ((TrackingEntry)b.DataContext).Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(view);
                Descendants<TextBlock>(view).Should().Contain(t => t.Text == name);
                Descendants<Button>(view).Single(b => b.Name == "AcquireUnknownButton"
                    && ((TrackingEntry)b.DataContext).Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(view);
                vm.IsTracked(name).Should().BeTrue();
                Descendants<Button>(view).Single(b => b.Name == "AcquireUnknownButton"
                    && ((TrackingEntry)b.DataContext).Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layout(view);
                vm.IsTracked(name).Should().BeFalse();
            }
            vm.AllEntries.Should().HaveCount(4).And.OnlyContain(e => e.IsAcquired);
            ((Run)view.FindName("PendingOperatorCount")).Text.Should().Be("0");
            ((Run)view.FindName("PendingEquipmentCount")).Text.Should().Be("0");
            ((FrameworkElement)view.FindName("EmptyState")).Visibility.Should().Be(Visibility.Collapsed);
            var shop = new List<ShopItem> { new() { Name = operatorName, IsTracked = true }, new() { Name = equipmentName, IsTracked = true } };
            GameStateUpdater.UpdateShopTracking(shop, vm.AllEntries.ToList());
            shop.Should().OnlyContain(i => !i.IsTracked);

            vm.AddTracking(operatorName, TrackingType.Operator);
            vm.AddTracking(equipmentName, TrackingType.Equipment);
            Layout(view);
            vm.AllEntries.Should().HaveCount(4);
            ((Run)view.FindName("PendingOperatorCount")).Text.Should().Be("1");
            ((Run)view.FindName("PendingEquipmentCount")).Text.Should().Be("1");
            ((Button)Descendants<OperatorCard>(view).Single().FindName("AcquiredButton")).IsEnabled.Should().BeTrue();
            ((Button)Descendants<TrackedEquipmentCard>(view).Single().FindName("AcquiredButton")).IsEnabled.Should().BeTrue();
            ((Button)Descendants<OperatorCard>(view).Single().FindName("TrackButton"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)Descendants<TrackedEquipmentCard>(view).Single().FindName("TrackButton"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(view);
            vm.AllEntries.Select(e => e.Name).Should().BeEquivalentTo("旧版已移除干员", "旧版已移除装备");
        }
        ThemeManager.Apply(false);
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(280, 850));
        view.Arrange(new Rect(0, 0, 280, 850));
        view.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Save(FrameworkElement view, string name)
    {
        var image = new RenderTargetBitmap(280, 500, 96, 96, PixelFormats.Pbgra32);
        image.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "acquired-previews");
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(file);
    }
}
