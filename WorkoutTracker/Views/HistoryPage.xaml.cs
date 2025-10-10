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
    private async void OnClearAllClicked(object sender, EventArgs e)
    {
        if (BindingContext is HistoryViewModel vm)
        {
            var confirm = await DisplayAlert(
                "Clear history",
                "Delete ALL sessions (and their sets)? This cannot be undone.",
                "Delete", "Cancel");

            if (confirm)
                await vm.ClearAllAsync();
        }
    }

}

