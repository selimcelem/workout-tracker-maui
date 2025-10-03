using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<WorkoutCategory>> GetAllAsync();
    Task<WorkoutCategory> AddAsync(string name);
    Task RenameAsync(int id, string newName);
    Task DeleteAsync(int id);

    // Seed defaults if DB empty
    Task SeedDefaultsAsync();
}
