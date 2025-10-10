using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface ISessionService
{
    Task<WorkoutSession> StartSessionAsync(string? notes = null);
    Task EndSessionAsync(int sessionId, string? notes = null);
    Task<WorkoutSession?> GetOpenSessionAsync();
    Task<List<WorkoutSession>> GetRecentAsync(int take = 20);
    Task DeleteAsync(int sessionId);
    Task DeleteAllSessionsAsync();
    Task<int> CountAsync();
    Task<int> GetDisplayNumberAsync(int sessionId);
}

public class SessionService : ISessionService
{
    private readonly SQLiteAsyncConnection _conn;
    private readonly ISetService _sets;

    public SessionService(SQLiteAsyncConnection conn, ISetService sets)
    {
        _conn = conn;
        _sets = sets;
    }

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
        var todayUtc = DateTime.UtcNow.Date;
        return await _conn.Table<WorkoutSession>()
            .Where(s => !s.IsClosed && s.DateUtc >= todayUtc)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    public Task<List<WorkoutSession>> GetRecentAsync(int take = 20) =>
        _conn.Table<WorkoutSession>()
             .OrderByDescending(s => s.DateUtc)
             .Take(take)
             .ToListAsync();

    public async Task DeleteAsync(int sessionId)
    {
        // delete sets first, then the session.
        await _sets.DeleteBySessionAsync(sessionId);
        await _conn.DeleteAsync<WorkoutSession>(sessionId);
    }

    public async Task DeleteAllSessionsAsync()
    {
        await _conn.RunInTransactionAsync(tran =>
        {
            tran.Execute("DELETE FROM SetEntry");
            tran.Execute("DELETE FROM WorkoutSession");
        });
    }

    public Task<int> CountAsync() =>
    _conn.Table<WorkoutSession>().CountAsync();

    public async Task<int> GetDisplayNumberAsync(int sessionId)
    {
        var s = await _conn.FindAsync<WorkoutSession>(sessionId);
        if (s is null) return 0;

        // ordinal by (DateUtc, then Id) so numbering is stable
        var sql = @"SELECT COUNT(*) FROM WorkoutSession
                WHERE DateUtc < ?
                   OR (DateUtc = ? AND Id <= ?)";
        return await _conn.ExecuteScalarAsync<int>(sql, s.DateUtc, s.DateUtc, sessionId);
    }
}
