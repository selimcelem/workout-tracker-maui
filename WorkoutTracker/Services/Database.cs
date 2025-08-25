using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public class Database
{
    private readonly SQLiteAsyncConnection _conn;
    public Database(SQLiteAsyncConnection conn) => _conn = conn;

    public async Task InitAsync()
    {
        await _conn.CreateTableAsync<Exercise>();
        await _conn.CreateTableAsync<WorkoutSession>();
        await _conn.CreateTableAsync<SetEntry>();

        // Seed example exercises on first run
        if (await _conn.Table<Exercise>().CountAsync() == 0)
        {
            await _conn.InsertAllAsync(new[]
            {
                new Exercise { Name = "Squat", BodyPart = "Legs" },
                new Exercise { Name = "Bench Press", BodyPart = "Chest" },
                new Exercise { Name = "Deadlift", BodyPart = "Back" },
            });
        }
    }
}
