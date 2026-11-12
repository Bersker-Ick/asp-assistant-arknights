// Modified for ASP Assistant Regulation on 2026-11-10; see MODIFICATIONS.md.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ASPAssistant.Core.Models;

namespace ASPAssistant.App.Controls;

public partial class TrackedEquipmentCard : UserControl
{
    public static readonly RoutedEvent TrackingToggledEvent = EventManager.RegisterRoutedEvent(
        "TrackingToggled", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TrackedEquipmentCard));

    public event RoutedEventHandler TrackingToggled
    {
        add => AddHandler(TrackingToggledEvent, value);
        remove => RemoveHandler(TrackingToggledEvent, value);
    }

    public static readonly RoutedEvent AcquiredEvent = EventManager.RegisterRoutedEvent(
        "Acquired", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TrackedEquipmentCard));

    public event RoutedEventHandler Acquired
    {
        add => AddHandler(AcquiredEvent, value);
        remove => RemoveHandler(AcquiredEvent, value);
    }

    public static readonly DependencyProperty IsAcquiredProperty = DependencyProperty.Register(
        nameof(IsAcquired), typeof(bool), typeof(TrackedEquipmentCard),
        new PropertyMetadata(false, (d, _) => ((TrackedEquipmentCard)d).UpdateAcquiredState()));

    public bool IsAcquired
    {
        get => (bool)GetValue(IsAcquiredProperty);
        set => SetValue(IsAcquiredProperty, value);
    }

    private void OnAcquiredClick(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(AcquiredEvent, this));

    public TrackedEquipmentCard()
    {
        InitializeComponent();
        HoverToolTipBehavior.Attach(CardBorder, DetailsToolTip);
        DataContextChanged += (_, _) =>
        {
            DetailsToolTip.IsOpen = false;
            UpdateAcquiredState();
            if (DataContext is not Equipment equipment) return;
            TypeText.Text = equipment.Type == EquipmentType.JobChange
                ? string.IsNullOrEmpty(equipment.JobChangeCovenant) ? "转职" : $"转职 · {equipment.JobChangeCovenant}"
                : "普通";
            TypeText.SetResourceReference(TextBlock.ForegroundProperty,
                equipment.Type == EquipmentType.JobChange ? "AccentBrush" : "TextSecondaryBrush");
        };
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        => DetailsToolTip.IsOpen = false;

    private void UpdateAcquiredState()
    {
        AcquiredButton.Tag = IsAcquired ? "acquired" : "";
        CardBorder.SetResourceReference(Border.BorderBrushProperty, IsAcquired ? "SuccessBorderBrush" : "BorderBrush");
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(TrackingToggledEvent, this));
}
