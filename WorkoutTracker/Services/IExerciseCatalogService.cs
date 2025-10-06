using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface IExerciseCatalogService
{
    Task<List<ExerciseCatalogEntry>> SearchAsync(string fragment, int limit = 20);
    Task SeedDefaultsAsync();
}
