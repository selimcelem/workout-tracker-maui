using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ISetService
{
    Task<int> GetNextSetNumberAsync(int sessionId, int exerciseId);
    Task<SetEntry> AddAsync(int sessionId, int exerciseId, int setNumber, int reps, double weight, double? rpe);
    Task<List<SetEntry>> GetBySessionAsync(int sessionId);
    Task DeleteAsync(int setId);
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

    public async Task<SetEntry> AddAsync(int sessionId, int exerciseId, int setNumber, int reps, double weight, double? rpe)
    {
        var entry = new SetEntry
        {
            SessionId = sessionId,
            ExerciseId = exerciseId,
            SetNumber = setNumber,
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
}
