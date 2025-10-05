using WorkoutTracker.ViewModels;
using WorkoutTracker.Helpers;

namespace WorkoutTracker.Views;

public partial class ExercisesPage : ContentPage
{
    private readonly ExercisesViewModel _vm;

    public ExercisesPage() : this(ServiceHelper.GetService<ExercisesViewModel>()) { }

    public ExercisesPage(ExercisesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Load();
    }
}
