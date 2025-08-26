using System.Linq;
using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ISetService
{
    Task<SetEntry> AddAsync(int sessionId, int exerciseId, int reps, double weight, double? rpe = null);
    Task<List<SetEntry>> GetBySessionAsync(int sessionId);
    Task DeleteAsync(int setId);
}

public class SetService : ISetService
{
    private readonly SQLiteAsyncConnection _conn;
    public SetService(SQLiteAsyncConnection conn) => _conn = conn;

    // Adds a set and auto-assigns SetNumber based on existing sets for THIS session+exercise
    public async Task<SetEntry> AddAsync(int sessionId, int exerciseId, int reps, double weight, double? rpe = null)
    {
        // Find the last set number for this exercise in this session
        var last = await _conn.Table<SetEntry>()
            .Where(s => s.SessionId == sessionId && s.ExerciseId == exerciseId)
            .OrderByDescending(s => s.SetNumber)
            .FirstOrDefaultAsync();

        int nextSetNumber = (last?.SetNumber ?? 0) + 1;

        var entry = new SetEntry
        {
            SessionId = sessionId,
            ExerciseId = exerciseId,
            SetNumber = nextSetNumber,
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
             .ThenBy(s => s.SetNumber)
             .ToListAsync();

    public Task DeleteAsync(int setId) => _conn.DeleteAsync<SetEntry>(setId);
}
