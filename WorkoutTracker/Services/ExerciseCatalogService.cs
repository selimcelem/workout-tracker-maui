using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public sealed class ExerciseCatalogService : IExerciseCatalogService
{
    private readonly SQLiteAsyncConnection _conn;
    public ExerciseCatalogService(SQLiteAsyncConnection conn) => _conn = conn;

    public async Task EnsureCreatedAsync()
    {
        await _conn.CreateTableAsync<ExerciseCatalogItem>();
    }

    public async Task SeedDefaultsAsync()
    {
        var count = await _conn.Table<ExerciseCatalogItem>().CountAsync();
        if (count > 0) return;

        var items = new[]
        {
            new ExerciseCatalogItem { Name = "Back Squat", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
            new ExerciseCatalogItem { Name = "Front Squat", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
            new ExerciseCatalogItem { Name = "Bench Press", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
            new ExerciseCatalogItem { Name = "Incline Bench Press", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
            new ExerciseCatalogItem { Name = "Deadlift", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
            new ExerciseCatalogItem { Name = "Overhead Press", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },
            new ExerciseCatalogItem { Name = "Pull-Up", BodyPart = "Back", IsCompound = true },
            new ExerciseCatalogItem { Name = "Bent-Over Row", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
            // Isolation examples with smaller jumps
            new ExerciseCatalogItem { Name = "Biceps Curl", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
            new ExerciseCatalogItem { Name = "Hammer Curl", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
            new ExerciseCatalogItem { Name = "Lateral Raise", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
            new ExerciseCatalogItem { Name = "Leg Extension", BodyPart = "Quads", IsCompound = false, DefaultIncrementKg = 1.25 },
            new ExerciseCatalogItem { Name = "Leg Curl", BodyPart = "Hamstrings", IsCompound = false, DefaultIncrementKg = 1.25 },
            new ExerciseCatalogItem { Name = "Calf Raise", BodyPart = "Calves", IsCompound = false, DefaultIncrementKg = 1.25 },
            new ExerciseCatalogItem { Name = "Chest Fly", BodyPart = "Chest", IsCompound = false, DefaultIncrementKg = 1.25 },
        };

        await _conn.InsertAllAsync(items);
    }

    public async Task<IReadOnlyList<ExerciseCatalogItem>> SearchAsync(string fragment, int limit = 15)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return Array.Empty<ExerciseCatalogItem>();
        fragment = fragment.Trim();

        var sql = $"SELECT * FROM ExerciseCatalogItem WHERE Name LIKE ? ORDER BY Name LIMIT {limit}";
        return await _conn.QueryAsync<ExerciseCatalogItem>(sql, $"{fragment}%");
    }
}
