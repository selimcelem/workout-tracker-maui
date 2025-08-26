using SQLite;
using System.Data.Common;
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
        // if an open session exists, return it
        var existing = await GetOpenSessionAsync();
        if (existing != null) return existing;

        var session = new WorkoutSession { DateUtc = DateTime.UtcNow, Notes = notes };
        await _conn.InsertAsync(session);
        return session;
    }

    public async Task EndSessionAsync(int sessionId, string? notes = null)
    {
        var s = await _conn.FindAsync<WorkoutSession>(sessionId);
        if (s is null) return;
        s.Notes = notes ?? s.Notes;
        await _conn.UpdateAsync(s);
    }

    public async Task<WorkoutSession?> GetOpenSessionAsync()
    {
        // “Open” just means the most recent session today; you can refine later
        var today = DateTime.UtcNow.Date;
        return await _conn.Table<WorkoutSession>()
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(s => s.DateUtc >= today);
    }

    public Task<List<WorkoutSession>> GetRecentAsync(int take = 20) =>
        _conn.Table<WorkoutSession>()
             .OrderByDescending(s => s.DateUtc)
             .Take(take)
             .ToListAsync();
}
