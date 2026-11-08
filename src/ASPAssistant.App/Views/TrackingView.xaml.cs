// Modified for ASP Assistant Regulation on 2026-11-09; see MODIFICATIONS.md.
using System.Windows;
using System.Windows.Controls;
using ASPAssistant.App.Controls;
using ASPAssistant.Core.Models;

namespace ASPAssistant.App.Views;

public partial class TrackingView : UserControl
{
    public event Action<string>? RemoveTrackingRequested;
    public event Action? ClearAllTrackingRequested;
    public static readonly DependencyProperty OperatorDatabaseProperty = DependencyProperty.Register(
        nameof(OperatorDatabase), typeof(IReadOnlyDictionary<string, Operator>), typeof(TrackingView),
        new PropertyMetadata(new Dictionary<string, Operator>()));

    public IReadOnlyDictionary<string, Operator> OperatorDatabase
        => (IReadOnlyDictionary<string, Operator>)GetValue(OperatorDatabaseProperty);

    public TrackingView()
    {
        InitializeComponent();
    }

    public void SetOperators(IReadOnlyList<Operator> operators)
        => SetValue(OperatorDatabaseProperty, operators.ToDictionary(o => o.Name));

    private void OnTrackedOperatorToggled(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is OperatorCard { DataContext: Operator op })
            RemoveTrackingRequested?.Invoke(op.Name);
    }

    private void OnRemoveTracking(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            RemoveTrackingRequested?.Invoke(name);
        }
    }

    private void OnClearAllTracking(object sender, RoutedEventArgs e)
    {
        ClearAllTrackingRequested?.Invoke();
    }
}
