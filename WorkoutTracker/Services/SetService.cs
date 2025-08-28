using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ISetService
{
    Task<SetEntry> AddAsync(int sessionId, int exerciseId, int reps, double weight, double? rpe = null);
    Task<List<SetEntry>> GetBySessionAsync(int sessionId);
    Task DeleteAsync(int setId);

    // NEW: fetch sets for an exercise since a UTC timestamp (for charts)
    Task<List<SetEntry>> GetByExerciseSinceAsync(int exerciseId, DateTime sinceUtc);
}

public class SetService : ISetService
{
    private readonly SQLiteAsyncConnection _conn;
    public SetService(SQLiteAsyncConnection conn) => _conn = conn;

    public async Task<int> GetNextSetNumberAsync(int sessionId, int exerciseId)
    {
        var last = await _conn.Table<SetEntry>()
            .Where(s => s.SessionId == sessionId && s.ExerciseId == exerciseId)
            .OrderByDescending(s => s.SetNumber)
            .FirstOrDefaultAsync();
        return (last?.SetNumber ?? 0) + 1;
    }

    public async Task<SetEntry> AddAsync(int sessionId, int exerciseId, int reps, double weight, double? rpe = null)
    {
        // next set number per session+exercise
        var next = await GetNextSetNumberAsync(sessionId, exerciseId);

        var entry = new SetEntry
        {
            SessionId = sessionId,
            ExerciseId = exerciseId,
            SetNumber = next,
            Reps = reps,
            Weight = weight,
            Rpe = rpe,
            TimestampUtc = DateTime.UtcNow
        };

        await _conn.InsertAsync(entry);
        return entry;
    }

    public Task<List<SetEntry>> GetBySessionAsync(int sessionId) =>
        _conn.Table<SetEntry>()
             .Where(s => s.SessionId == sessionId)
             .OrderBy(s => s.TimestampUtc)
             .ToListAsync();

    public Task DeleteAsync(int setId) => _conn.DeleteAsync<SetEntry>(setId);

    // NEW
    public Task<List<SetEntry>> GetByExerciseSinceAsync(int exerciseId, DateTime sinceUtc) =>
        _conn.Table<SetEntry>()
             .Where(s => s.ExerciseId == exerciseId && s.TimestampUtc >= sinceUtc)
             .OrderBy(s => s.TimestampUtc)
             .ToListAsync();
}
