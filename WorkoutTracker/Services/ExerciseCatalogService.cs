using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public class ExerciseCatalogService : IExerciseCatalogService
{
    private readonly SQLiteAsyncConnection _conn;
    public ExerciseCatalogService(SQLiteAsyncConnection conn) => _conn = conn;

    public async Task<List<ExerciseCatalogEntry>> SearchAsync(string fragment, int limit = 20)
    {
        var q = (fragment ?? "").Trim();
        if (q.Length == 0) return new();
        // starts-with first, case-insensitive
        return await _conn.QueryAsync<ExerciseCatalogEntry>(
            @"SELECT * FROM ExerciseCatalogEntry
              WHERE Name LIKE ? COLLATE NOCASE
              ORDER BY Name
              LIMIT ?", q + "%", limit);
    }

    public async Task SeedDefaultsAsync()
    {
        await _conn.CreateTableAsync<ExerciseCatalogEntry>();
        var count = await _conn.Table<ExerciseCatalogEntry>().CountAsync();
        if (count > 0) return;

        var defaults = new[]
        {
            "Back Extension","Barbell Back Squat","Barbell Bench Press","Barbell Curl",
            "Bent-Over Row","Bicep Curl","Bulgarian Split Squat","Calf Raise","Deadlift",
            "Dumbbell Bench Press","Dumbbell Curl","Face Pull","Hip Thrust","Incline Bench Press",
            "Lat Pulldown","Leg Curl","Leg Extension","Overhead Press","Pull-Up","Romanian Deadlift",
            "Seated Row","Shoulder Press","Tricep Pushdown","Tricep Extension"
        }.Distinct(StringComparer.OrdinalIgnoreCase)
         .Select(n => new ExerciseCatalogEntry { Name = n })
         .ToList();

        await _conn.InsertAllAsync(defaults);
    }
}
