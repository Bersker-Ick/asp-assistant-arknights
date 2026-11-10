using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ASPAssistant.App;
using ASPAssistant.App.Controls;
using ASPAssistant.App.Views;
using ASPAssistant.App.Windows;
using ASPAssistant.Core.Data;
using ASPAssistant.Core.Models;
using ASPAssistant.Core.Services;
using ASPAssistant.Core.ViewModels;
using FluentAssertions;

namespace ASPAssistant.Tests.Views;

public class ThemeAndHighlightTests
{
    [Fact]
    public void ThemeSwitch_UpdatesControlsAndPreservesHighlightAndSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var host = new Window { ShowActivated = false, ShowInTaskbar = false };
            try
            {
                foreach (var resource in new[] { "Themes/Dark.xaml", "Styles.xaml", "Logo.xaml" })
                    application.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"/ASPAssistant.App;component/Resources/{resource}", UriKind.Relative)
                    });

                var store = new JsonDataStore(Path.Combine(AppContext.BaseDirectory, "TestData", "Regulation"));
                var vm = new OperatorBrowseViewModel();
                var operators = store.LoadOperatorsAsync().GetAwaiter().GetResult();
                vm.LoadOperators(operators);
                var view = new OperatorBrowseView { DataContext = vm };
                host.Content = view;
                var toggle = (System.Windows.Controls.Primitives.ToggleButton)view.FindName("FilterToggle");
                toggle.IsChecked = true;
                var coreList = (ListBox)view.FindName("CoreCovenantFilterList");
                var output = Path.Combine(AppContext.BaseDirectory, "TestResults", "theme-previews");
                Directory.CreateDirectory(output);

                foreach (var isLight in new[] { false, true })
                {
                    ThemeManager.Apply(isLight);
                    vm.SetAvailableCovenants(["乌萨斯", "奥术"]);
                    view.Measure(new Size(310, 900));
                    view.Arrange(new Rect(0, 0, 310, 900));
                    view.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                    var item = (ListBoxItem)coreList.ItemContainerGenerator.ContainerFromItem("乌萨斯");
                    item.IsSelected = true;
                    item.ApplyTemplate();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var chip = (Border)item.Template.FindName("bg", item);
                    item.Tag.Should().Be("available");
                    ((SolidColorBrush)chip.BorderBrush).Color.Should().Be(
                        ((SolidColorBrush)application.FindResource("AvailableBrush")).Color);
                    // Selected chips use the theme's filter-selection palette: orange on the
                    // light theme, accent blue on the dark theme.
                    var selectedText = ((SolidColorBrush)application.FindResource("FilterSelectedBrush")).Color;
                    selectedText.Should().Be(isLight
                        ? Color.FromRgb(0xB4, 0x53, 0x09)
                        : Color.FromRgb(0x4F, 0xC3, 0xF7));
                    ((SolidColorBrush)chip.Background).Color.Should().Be(
                        ((SolidColorBrush)application.FindResource("FilterSelectedTintBrush")).Color);
                    ((SolidColorBrush)view.Background).Color.Should().Be(isLight
                        ? Color.FromRgb(247, 249, 252) : Color.FromRgb(26, 26, 46));

                    var scroll = Descendants<ScrollViewer>(view).Single(s => s.ScrollableHeight > 0);
                    scroll.ScrollToVerticalOffset(100);
                    view.UpdateLayout();
                    toggle.IsChecked.Should().BeTrue();
                    scroll.ScrollToTop();
                    view.UpdateLayout();

                    var firstCard = Descendants<OperatorCard>(view).First();
                    var tooltip = (ToolTip)firstCard.FindName("DetailsToolTip");
                    tooltip.PlacementTarget = (UIElement)firstCard.FindName("CardBorder");
                    tooltip.Measure(new Size(500, 700));
                    tooltip.Arrange(new Rect(tooltip.DesiredSize));
                    tooltip.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var labels = Descendants<TextBlock>(tooltip).Select(t => t.Text).ToList();
                    labels.Should().Contain("普通").And.Contain("精锐");
                    Descendants<ContentControl>(tooltip).Should().Contain(c => c.Tag as string == "精锐");
                    var detailCard = Descendants<Border>(tooltip).First(b => b.Width == 228);
                    ((SolidColorBrush)detailCard.Background).Color.Should().Be(isLight
                        ? Colors.White : Color.FromRgb(37, 37, 64));

                    Save(view, Path.Combine(output, isLight ? "light.png" : "dark.png"));
                    Save(tooltip, Path.Combine(output, isLight ? "light-details.png" : "dark-details.png"));

                    ((Button)view.FindName("ClearHighlightsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    vm.HighlightedCovenants.Should().BeEmpty();
                    item.IsSelected.Should().BeTrue();
                    item.Tag.Should().Be("");
                    vm.ActiveFilterTags.Should().Contain("乌萨斯");
                    ((SolidColorBrush)chip.BorderBrush).Color.Should().Be(
                        ((SolidColorBrush)application.FindResource("FilterSelectedBrush")).Color);
                }
                ThemeManager.Apply(false);
                ((SolidColorBrush)view.Background).Color.Should().Be(Color.FromRgb(26, 26, 46));

                var (equipment, manualJobChanges) = store.LoadEquipmentAsync().GetAwaiter().GetResult();
                var (covenants, mappings) = store.LoadCovenantsAsync().GetAwaiter().GetResult();
                var equipmentVm = new EquipmentBrowseViewModel();
                equipmentVm.LoadEquipment(equipment,
                    operators.SelectMany(o => o.CoreCovenants.Concat(o.AdditionalCovenants)), manualJobChanges, mappings);
                var lineupVm = new LineupViewModel(new LineupStore(Path.Combine(output, "lineups")),
                    new CovenantCalculator(operators, covenants, mappings));
                var trackingVm = new TrackingViewModel();
                trackingVm.AddTracking("烈夏", TrackingType.Operator);
                trackingVm.AddTracking("塔露拉", TrackingType.Operator);
                trackingVm.AddTracking("乌萨斯呼吸器", TrackingType.Equipment);
                var enemyVm = new EnemyViewModel();
                var sidePanel = new SidePanelWindow(vm, equipmentVm, trackingVm,
                    new GameStateViewModel(), new BanViewModel(), enemyVm,
                    lineupVm, new LineupShareService(), operators, equipment, new SettingsManager(output), GameShopTheme.Terminal);
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    sidePanel.SetGameMinimized(true);
                    sidePanel.WindowState.Should().Be(WindowState.Minimized);
                    sidePanel.SetGameMinimized(false);
                    sidePanel.WindowState.Should().Be(WindowState.Normal);
                    sidePanel.WindowState = WindowState.Minimized;
                    sidePanel.SetGameMinimized(true);
                    sidePanel.SetGameMinimized(false);
                    sidePanel.WindowState.Should().Be(WindowState.Minimized,
                        "a manually minimized assistant must stay minimized when the game restores");
                    sidePanel.WindowState = WindowState.Normal;
                    var content = (Border)sidePanel.Content;
                    var tabs = Descendants<TabControl>(content).Single();
                    var settingsButton = (Button)sidePanel.FindName("SettingsButton");
                    settingsButton.Content.Should().Be("⚙");
                    sidePanel.FindName("ThemeToggleButton").Should().BeNull();
                    var settingsPanel = (Border)sidePanel.FindName("SettingsPanel");
                    var uiThemes = (ListBox)sidePanel.FindName("UiThemeSelector");
                    var gameThemes = (ListBox)sidePanel.FindName("GameThemeSelector");
                    var multiToggle = (CheckBox)sidePanel.FindName("MultiLineupTrackingToggle");
                    gameThemes.Items.Cast<ListBoxItem>().Select(i => i.Content).Should()
                        .Equal("卫戍协议终端", "乌萨斯齐心团", "“全能的”可露希尔");
                    gameThemes.SelectedIndex.Should().Be(0);
                    GameShopTheme changedTheme = GameShopTheme.Terminal;
                    sidePanel.GameThemeChanged += theme => changedTheme = theme;
                    foreach (var isLight in new[] { false, true })
                    {
                        ThemeManager.Apply(isLight);
                        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        settingsPanel.Visibility.Should().Be(Visibility.Visible);
                        uiThemes.SelectedIndex = isLight ? 1 : 0;
                        WaitForPreferenceSave(uiThemes);
                        ThemeManager.IsLight.Should().Be(isLight);
                        foreach (var theme in Enum.GetValues<GameShopTheme>())
                        {
                            gameThemes.SelectedIndex = (int)theme;
                            WaitForPreferenceSave(gameThemes);
                            sidePanel.GameTheme.Should().Be(theme);
                            changedTheme.Should().Be(theme);
                        }
                        gameThemes.SelectedIndex = 0;
                        WaitForPreferenceSave(gameThemes);
                        multiToggle.IsChecked = true;
                        WaitForPreferenceSave(multiToggle);
                        trackingVm.TrackMultipleLineups.Should().BeTrue();
                        File.ReadAllText(Path.Combine(output, "game-theme.json")).Should().Be("\"Terminal\"");
                        File.ReadAllText(Path.Combine(output, "multiple-lineups.json")).Should().Be("true");
                        content.Measure(new Size(310, 900));
                        content.Arrange(new Rect(0, 0, 310, 900));
                        content.UpdateLayout();
                        Save(content, Path.Combine(output, isLight ? "light-settings.png" : "dark-settings.png"));
                        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        settingsPanel.Visibility.Should().Be(Visibility.Collapsed);
                        for (var i = 0; i < tabs.Items.Count; i++)
                        {
                            tabs.SelectedIndex = i;
                            content.Measure(new Size(310, 900));
                            content.Arrange(new Rect(0, 0, 310, 900));
                            content.UpdateLayout();
                            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                            var page = (UserControl)((TabItem)tabs.Items[i]).Content;
                            page.ActualWidth.Should().BeGreaterThan(290);
                            ((SolidColorBrush)page.Background).Color.Should().Be(isLight
                                ? Color.FromRgb(247, 249, 252) : Color.FromRgb(26, 26, 46));
                            if (page is EnemyView enemyView)
                            {
                                var button = (Button)enemyView.FindName("RecognizeEnemyButton");
                                enemyVm.BeginRecognition();
                                content.UpdateLayout();
                                button.IsEnabled.Should().BeFalse();
                                ((TextBlock)enemyView.FindName("RecognitionStatusText")).Text.Should().StartWith("正在");
                                enemyVm.Update(new("假想敌：鳍", ["特训敌人·元素", "特训敌人·致命"],
                                    ["假想敌：鳍", "特训敌人·元素", "特训敌人·致命"]),
                                    null, [new() { TypeName = "元素" }, null]);
                                content.UpdateLayout();
                                button.IsEnabled.Should().BeTrue();
                                Descendants<TextBlock>(enemyView).Should().Contain(t => t.Text.Contains("致命：资料未收录"));
                                int requested = 0;
                                Action onRequest = () => requested++;
                                sidePanel.EnemyRecognitionRequested += onRequest;
                                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                requested.Should().Be(1);
                                sidePanel.EnemyRecognitionRequested -= onRequest;
                            }
                            if (page is EquipmentBrowseView equipmentView)
                            {
                                ((ListBox)equipmentView.FindName("TypeFilterList")).Items.Cast<string>()
                                    .Should().Equal("", "普通", "转职");
                                ((ListBox)equipmentView.FindName("CategoryFilterList")).Items.Cast<string>()
                                    .Should().Equal("", "攻击", "防御", "生存", "技力", "经济", "盟约", "其他");
                                // “同时选中”复选框 + 空列表提示
                                var categoryMatchAll = (CheckBox)equipmentView.FindName("CategoryMatchAllToggle");
                                categoryMatchAll.Should().NotBeNull();
                                var emptyText = (TextBlock)equipmentView.FindName("EmptyFilterText");
                                emptyText.Visibility.Should().Be(Visibility.Collapsed);
                                equipmentVm.SelectedCategoryFilters = ["技力", "经济"];
                                content.UpdateLayout();
                                equipmentVm.FilteredEquipment.Should().HaveCount(9);
                                categoryMatchAll.IsChecked = true;
                                content.UpdateLayout();
                                equipmentVm.CategoryMatchAll.Should().BeTrue();
                                equipmentVm.FilteredEquipment.Should().BeEmpty();
                                emptyText.Visibility.Should().Be(Visibility.Visible);
                                emptyText.Text.Should().Be("没有同时满足所选条件的装备");
                                categoryMatchAll.IsChecked = false;
                                equipmentVm.SelectedCategoryFilters = [];
                                content.UpdateLayout();
                                emptyText.Visibility.Should().Be(Visibility.Collapsed);
                                ((System.Windows.Controls.Primitives.ToggleButton)equipmentView.FindName("FilterToggle")).IsChecked = true;
                                content.UpdateLayout();
                                var tiers = (ListBox)equipmentView.FindName("TierFilterList");
                                tiers.SelectedIndex = 1;
                                content.UpdateLayout();
                                equipmentVm.SelectedTierFilters.Should().ContainSingle();
                                equipmentVm.FilteredEquipment.Should().HaveCountLessThan(equipment.Count);
                                var allTier = (ListBoxItem)tiers.ItemContainerGenerator.ContainerFromIndex(0);
                                var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                                {
                                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                                    Source = Descendants<TextBlock>(allTier).Single()
                                };
                                tiers.RaiseEvent(click);
                                content.UpdateLayout();
                                click.Handled.Should().BeTrue();
                                tiers.SelectedIndex.Should().Be(0);
                                equipmentVm.SelectedTierFilters.Should().BeEmpty();
                                equipmentVm.FilteredEquipment.Should().HaveCount(equipment.Count);

                                var cards = Descendants<EquipmentCard>(equipmentView).ToList();
                                cards.Should().HaveCount(70);
                                var hammer = cards.Single(c => ((Equipment)c.DataContext).Name == "维式重锤");
                                var effect = (TextBlock)hammer.FindName("EffectText");
                                new TextRange(effect.ContentStart, effect.ContentEnd).Text.Should().Be("攻击力+15(25%)%");
                                var highlighted = effect.Inlines.OfType<Run>().Single(r => r.Text == "15");
                                highlighted.FontWeight.Should().Be(FontWeights.Bold);
                                ((SolidColorBrush)highlighted.Foreground).Color.Should().Be(
                                    ((SolidColorBrush)application.FindResource("AccentBrush")).Color);
                                hammer.FindName("NormalToggle").Should().BeNull();
                                hammer.FindName("EliteToggle").Should().BeNull();
                                ((TextBlock)hammer.FindName("NoEliteText")).Visibility.Should().Be(Visibility.Collapsed);
                                var name = (TextBlock)hammer.FindName("NameText");
                                var icon = (Image)hammer.FindName("IconImage");
                                (name.TranslatePoint(new Point(), hammer).Y + name.ActualHeight)
                                    .Should().BeLessThanOrEqualTo(icon.TranslatePoint(new Point(), hammer).Y);

                                var ultimate = cards.Single(c => ((Equipment)c.DataContext).Name == "终极维式重锤");
                                var noElite = (TextBlock)ultimate.FindName("NoEliteText");
                                noElite.Visibility.Should().Be(Visibility.Visible);
                                var jobChange = (TextBlock)ultimate.FindName("JobChangeText");
                                jobChange.Text.Should().Be("转职：维多利亚");
                                jobChange.Visibility.Should().Be(Visibility.Visible);
                                jobChange.TranslatePoint(new Point(), ultimate).Y.Should()
                                    .BeGreaterThan(noElite.TranslatePoint(new Point(), ultimate).Y);
                                var deposit = cards.Single(c => ((Equipment)c.DataContext).Name == "定期存折");
                                ((TextBlock)deposit.FindName("NoEliteText")).Visibility.Should().Be(Visibility.Visible);
                                ((TextBlock)deposit.FindName("JobChangeText")).Visibility.Should().Be(Visibility.Collapsed);
                                var projector = cards.Single(c => ((Equipment)c.DataContext).Name == "博士投影");
                                var projectorEffect = (TextBlock)projector.FindName("EffectText");
                                new TextRange(projectorEffect.ContentStart, projectorEffect.ContentEnd).Text.Should()
                                    .Be("普通：装备后下个回合开始时销毁，并使该干员晋升为精锐干员\n精锐：装备时销毁，立即使该干员晋升为精锐干员");

                                foreach (var (equipmentName, expected) in new[]
                                {
                                    ("萨尔贡浓茶", "技力自然回复速度+0.15(0.25)/秒"),
                                    ("阿戈尔重刃", "攻击力+40(60%)%，攻击速度-10"),
                                    ("叙拉古正装", "攻击速度+15(25)，携带者部署方向左右两侧的我方干员攻击速度+10(15)")
                                })
                                {
                                    var card = cards.Single(c => ((Equipment)c.DataContext).Name == equipmentName);
                                    var description = (TextBlock)card.FindName("EffectText");
                                    new TextRange(description.ContentStart, description.ContentEnd).Text.Should().Be(expected);
                                }

                                var samples = new StackPanel { Width = 294, Background = equipmentView.Background };
                                foreach (var equipmentName in new[] { "维式重锤", "终极维式重锤", "定期存折", "博士投影" })
                                    samples.Children.Add(new EquipmentCard { DataContext = equipment.Single(e => e.Name == equipmentName) });
                                samples.Measure(new Size(294, double.PositiveInfinity));
                                samples.Arrange(new Rect(samples.DesiredSize));
                                samples.UpdateLayout();
                                Save(samples, Path.Combine(output, isLight ? "light-equipment-samples.png" : "dark-equipment-samples.png"));
                            }
                            if (page is TrackingView trackingView)
                            {
                                trackingView.FindName("MultiLineupTrackingToggle").Should().BeNull();
                                trackingVm.TrackMultipleLineups.Should().BeTrue();
                                var trackedCards = Descendants<OperatorCard>(trackingView).ToList();
                                trackedCards.Should().HaveCount(2);
                                trackedCards.Should().OnlyContain(c => c.IsTrackingList && c.IsTracked);
                                var trackedCard = trackedCards.Single(c => ((Operator)c.DataContext).Name == "烈夏");
                                var portrait = (System.Windows.Shapes.Ellipse)trackedCard.FindName("TrackingPortrait");
                                portrait.Visibility.Should().Be(Visibility.Visible);
                                ((ImageBrush)portrait.Fill).ImageSource.Should().NotBeNull();
                                ((IEnumerable<CovenantChipItem>)((ItemsControl)trackedCard.FindName("CovenantChips")).ItemsSource)
                                    .Should().Contain(c => c.Name == "乌萨斯" && c.IsCore);
                                ((Button)trackedCard.FindName("BanButton")).Visibility.Should().Be(Visibility.Collapsed);
                                var detail = (ToolTip)trackedCard.FindName("DetailsToolTip");
                                detail.PlacementTarget = (UIElement)trackedCard.FindName("CardBorder");
                                detail.Measure(new Size(500, 700));
                                detail.Arrange(new Rect(detail.DesiredSize));
                                detail.UpdateLayout();
                                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                                Descendants<TextBlock>(detail).Select(t => t.Text)
                                    .Should().Contain("普通").And.Contain("精锐");
                                ((Button)trackedCard.FindName("TrackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                trackingVm.IsTracked("烈夏").Should().BeFalse();
                                trackingVm.IsTracked("塔露拉").Should().BeTrue();
                                trackingVm.AddTracking("烈夏", TrackingType.Operator);
                                trackingVm.AddTracking("旧版已移除干员", TrackingType.Operator);
                                content.UpdateLayout();
                                Descendants<TextBlock>(trackingView).Should().Contain(t => t.Text == "旧版已移除干员");
                                var removeUnknown = Descendants<Button>(trackingView)
                                    .Single(b => b.Tag as string == "旧版已移除干员");
                                removeUnknown.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                trackingVm.IsTracked("旧版已移除干员").Should().BeFalse();
                                content.UpdateLayout();
                            }
                            Save(content, Path.Combine(output, $"{(isLight ? "light" : "dark")}-page-{i}.png"));
                        }
                    }
                }
                finally
                {
                    sidePanel.Close();
                }
            }
            catch (Exception ex)
            {
                // Transfer assertion and WPF failures from the required STA thread to xUnit.
                failure = ex;
            }
            finally
            {
                host.Close();
                application.Shutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("WPF layout should finish without blocking");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void WaitForPreferenceSave(UIElement control)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!control.IsEnabled && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Thread.Sleep(1);
        }
        control.IsEnabled.Should().BeTrue("the preference write should complete");
    }

    private static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
