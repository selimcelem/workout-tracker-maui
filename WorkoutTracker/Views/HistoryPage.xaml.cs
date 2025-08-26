using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HistoryPage : ContentPage
{
    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HistoryViewModel m)
            m.LoadCommand.Execute(null);
    }
}
