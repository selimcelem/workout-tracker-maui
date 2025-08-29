using WorkoutTracker.ViewModels;
using WorkoutTracker.Helpers;

namespace WorkoutTracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    // Shell/HotReload-friendly ctor
    public HistoryPage() : this(ServiceHelper.GetService<HistoryViewModel>()) { }

    // DI ctor (kept for tests/manual resolve)
    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.RecentSessions.Any())
            await _vm.LoadAsync();
    }

    // Tap handler for each row
    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        // Prefer binding context of the tapped Grid
        if (sender is BindableObject bo && bo.BindingContext is SessionListItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(SessionDetailPage)}?sessionId={item.Id}");
            return;
        }

        // Fallback: if a CommandParameter was set (we didn't use it)
        if (e.Parameter is SessionListItem paramItem)
        {
            await Shell.Current.GoToAsync($"{nameof(SessionDetailPage)}?sessionId={paramItem.Id}");
        }
    }
}
