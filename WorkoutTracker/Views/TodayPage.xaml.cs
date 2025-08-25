using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class TodayPage : ContentPage
{
    public TodayPage(TodayViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TodayViewModel m)
            m.LoadCommand.Execute(null);
    }
}
