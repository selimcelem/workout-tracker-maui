using System.Diagnostics;
using WorkoutTracker.Helpers;
using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ChartsPage : ContentPage
{
    private readonly ChartsViewModel _vm;

    // Shell/HotReload-friendly parameterless ctor
    public ChartsPage() : this(ServiceHelper.GetService<ChartsViewModel>()) { }

    // DI ctor
    public ChartsPage(ChartsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("ChartsPage OnAppearing error: " + ex);
            await DisplayAlert("Charts error", ex.ToString(), "OK");
        }
    }
}
