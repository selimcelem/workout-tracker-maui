using WorkoutTracker.ViewModels;
using WorkoutTracker.Helpers;

namespace WorkoutTracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    // Shell/HotReload-friendly parameterless ctor
    public HistoryPage() : this(ServiceHelper.GetService<HistoryViewModel>()) { }

    // DI ctor (kept for unit tests and when manually resolving)
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
            if (!_vm.RecentSessions.Any())
                await _vm.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("HistoryPage OnAppearing error: " + ex);
            await DisplayAlert("History error", ex.ToString(), "OK");
        }
    }


    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is SessionListItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(SessionDetailPage)}?sessionId={item.Id}");
        }

        if (sender is CollectionView cv)
            cv.SelectedItem = null;
    }
}
