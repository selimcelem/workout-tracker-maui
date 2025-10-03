using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public class CategoryService : ICategoryService
{
    private readonly SQLiteAsyncConnection _conn;

    public CategoryService(SQLiteAsyncConnection conn)
    {
        _conn = conn;
    }

    public async Task<IReadOnlyList<WorkoutCategory>> GetAllAsync()
        => await _conn.Table<WorkoutCategory>()
                      .OrderBy(c => c.Name)
                      .ToListAsync();

    public async Task<WorkoutCategory> AddAsync(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Category name is required.", nameof(name));

        var cat = new WorkoutCategory { Name = trimmed };
        await _conn.InsertAsync(cat);
        return cat;
    }

    public Task RenameAsync(int id, string newName)
    {
        var trimmed = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("New name is required.", nameof(newName));

        return _conn.ExecuteAsync("UPDATE WorkoutCategory SET Name = ? WHERE Id = ?", trimmed, id);
    }

    public Task DeleteAsync(int id)
        => _conn.ExecuteAsync("DELETE FROM WorkoutCategory WHERE Id = ?", id);

    public async Task SeedDefaultsAsync()
    {
        // If any categories already exist, don’t reseed
        var any = await _conn.Table<WorkoutCategory>().FirstOrDefaultAsync();
        if (any != null) return;

        // Insert defaults
        await AddAsync("Push Day");
        await AddAsync("Pull Day");
        await AddAsync("Leg Day");
    }
}
