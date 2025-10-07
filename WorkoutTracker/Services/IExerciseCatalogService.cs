using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface IExerciseCatalogService
{
    Task EnsureCreatedAsync();
    Task SeedDefaultsAsync();
    Task<IReadOnlyList<ExerciseCatalogItem>> SearchAsync(string fragment, int limit = 15);
}
