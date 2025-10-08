using Microsoft.Maui.Storage;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public sealed class SettingsService : ISettingsService
{
    private const string Key = "training_goal";
    public TrainingGoal Goal
    {
        get => (TrainingGoal)Preferences.Get(Key, (int)TrainingGoal.Hypertrophy);
        set => Preferences.Set(Key, (int)value);
    }
}
