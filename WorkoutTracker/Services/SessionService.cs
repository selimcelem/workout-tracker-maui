using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ISessionService
{
    Task<WorkoutSession> StartSessionAsync(string? notes = null);
    Task EndSessionAsync(int sessionId, string? notes = null);
    Task<WorkoutSession?> GetOpenSessionAsync();
    Task<List<WorkoutSession>> GetRecentAsync(int take = 20);
}

public class SessionService : ISessionService
{
    private readonly SQLiteAsyncConnection _conn;
    public SessionService(SQLiteAsyncConnection conn) => _conn = conn;

    public async Task<WorkoutSession> StartSessionAsync(string? notes = null)
    {
        // If an open (not closed) session exists today, return it
        var existing = await GetOpenSessionAsync();
        if (existing != null) return existing;

        var session = new WorkoutSession
        {
            DateUtc = DateTime.UtcNow,
            Notes = notes,
            IsClosed = false
        };
        await _conn.InsertAsync(session);
        return session;
    }

    public async Task EndSessionAsync(int sessionId, string? notes = null)
    {
        var s = await _conn.FindAsync<WorkoutSession>(sessionId);
        if (s is null) return;

        s.Notes = notes ?? s.Notes;
        s.IsClosed = true;
        await _conn.UpdateAsync(s);
    }

    public async Task<WorkoutSession?> GetOpenSessionAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _conn.Table<WorkoutSession>()
            .Where(s => !s.IsClosed && s.DateUtc >= today)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public Task<List<WorkoutSession>> GetRecentAsync(int take = 20) =>
        _conn.Table<WorkoutSession>()
             .OrderByDescending(s => s.DateUtc)
             .Take(take)
             .ToListAsync();
}
