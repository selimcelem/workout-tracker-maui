namespace WorkoutTracker.Services;

using WorkoutTracker.Models;

public interface ISettingsService
{
    TrainingGoal Goal { get; set; }
}
