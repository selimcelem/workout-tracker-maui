using WorkoutTracker.ViewModels;
using WorkoutTracker.Helpers;

namespace WorkoutTracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

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
            await _vm.LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("History error", ex.Message, "OK");
        }
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is SessionListItem item)
        {
            await Shell.Current.GoToAsync($"{nameof(SessionDetailPage)}?sessionId={item.Id}");
        }
        if (sender is CollectionView cv) cv.SelectedItem = null;
    }
}
