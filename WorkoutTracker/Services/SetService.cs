// File: WorkoutTracker/Services/SetService.cs
using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ISetService
{
    Task<SetEntry> AddAsync(int sessionId, int exerciseId, int reps, double weight, double? rpe = null);
    Task<List<SetEntry>> GetBySessionAsync(int sessionId);
    Task DeleteAsync(int setId);

    // NEW: fetch sets for an exercise since a given UTC date
    Task<List<SetEntry>> GetByExerciseSinceAsync(int exerciseId, DateTime sinceUtc);

    // All sets for the last session in which this exercise appeared (or empty if none)
    Task<IReadOnlyList<SetEntry>> GetLastSessionSetsForExerciseAsync(int exerciseId);
    Task DeleteBySessionAsync(int sessionId);
}

public class SetService : ISetService
{
    private readonly SQLiteAsyncConnection _conn;
    public SetService(SQLiteAsyncConnection conn) => _conn = conn;

    private async Task<int> GetNextSetNumberAsync(int sessionId, int exerciseId)
    {
        var last = await _conn.Table<SetEntry>()
            .Where(s => s.SessionId == sessionId && s.ExerciseId == exerciseId)
            .OrderByDescending(s => s.SetNumber)
            .FirstOrDefaultAsync();
        return (last?.SetNumber ?? 0) + 1;
    }

    public async Task<SetEntry> AddAsync(int sessionId, int exerciseId, int reps, double weight, double? rpe = null)
    {
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

    public Task<List<SetEntry>> GetByExerciseSinceAsync(int exerciseId, DateTime sinceUtc) =>
        _conn.Table<SetEntry>()
             .Where(s => s.ExerciseId == exerciseId && s.TimestampUtc >= sinceUtc)
             .OrderBy(s => s.TimestampUtc)
             .ToListAsync();

    // NEW: all sets for the most recent session in which this exercise appeared
    public async Task<IReadOnlyList<SetEntry>> GetLastSessionSetsForExerciseAsync(int exerciseId)
    {
        // 1) find the latest set for this exercise
        var latest = await _conn.Table<SetEntry>()
                                .Where(s => s.ExerciseId == exerciseId)
                                .OrderByDescending(s => s.TimestampUtc)
                                .FirstOrDefaultAsync();

        if (latest == null)
            return Array.Empty<SetEntry>();

        // 2) get all sets from that same session
        var sets = await _conn.Table<SetEntry>()
                              .Where(s => s.SessionId == latest.SessionId && s.ExerciseId == exerciseId)
                              .OrderBy(s => s.SetNumber)
                              .ToListAsync();

        return sets;
    }
    public Task DeleteBySessionAsync(int sessionId) =>
    _conn.Table<SetEntry>()
         .DeleteAsync(s => s.SessionId == sessionId);
}
