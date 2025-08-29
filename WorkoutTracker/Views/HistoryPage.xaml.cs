using WorkoutTracker.ViewModels;
using WorkoutTracker.Helpers;

namespace WorkoutTracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    // Shell/HotReload-friendly ctor
    public HistoryPage() : this(ServiceHelper.GetService<HistoryViewModel>()) { }

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.LoadAsync(); // <-- always reload
        }
        catch (Exception ex)
        {
            await DisplayAlert("History error", ex.Message, "OK");
        }
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is SessionListItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(SessionDetailPage)}?sessionId={item.Id}");
        }
        else if (e.Parameter is SessionListItem paramItem)
        {
            await Shell.Current.GoToAsync($"{nameof(SessionDetailPage)}?sessionId={paramItem.Id}");
        }
    }
}
