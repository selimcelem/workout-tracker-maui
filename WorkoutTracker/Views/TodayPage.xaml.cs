using WorkoutTracker.ViewModels;
using WorkoutTracker.Models;

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
        {
            m.LoadCommand.Execute(null);

            GoalPicker.SelectedIndex = m.SelectedGoal switch
            {
                TrainingGoal.Strength => 0,
                TrainingGoal.Hypertrophy => 1,
                TrainingGoal.Endurance => 2,
                TrainingGoal.NoRecommendation => 3,
                _ => 1
            };
        }
    }

    private void OnGoalChanged(object sender, EventArgs e)
    {
        if (BindingContext is TodayViewModel vm &&
            sender is Picker p && p.SelectedIndex >= 0)
        {
            vm.SelectedGoal = p.SelectedIndex switch
            {
                0 => TrainingGoal.Strength,
                1 => TrainingGoal.Hypertrophy,
                2 => TrainingGoal.Endurance,
                3 => TrainingGoal.NoRecommendation,
                _ => TrainingGoal.Hypertrophy
            };
        }
    }
}
