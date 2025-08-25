using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ExercisesPage : ContentPage
{
    public ExercisesPage(ExercisesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;   // DI-provided ViewModel
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ExercisesViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
