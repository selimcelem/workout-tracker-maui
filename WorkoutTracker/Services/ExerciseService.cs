using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface IExerciseService
{
    Task<List<Exercise>> GetAllAsync();
    Task AddAsync(string name, string? bodyPart = null, string? notes = null);
    Task DeleteAsync(int id);
}

public class ExerciseService : IExerciseService
{
    private readonly SQLiteAsyncConnection _conn;
    public ExerciseService(SQLiteAsyncConnection conn) => _conn = conn;

    public Task<List<Exercise>> GetAllAsync()
        => _conn.Table<Exercise>().OrderBy(e => e.Name).ToListAsync();

    public Task AddAsync(string name, string? bodyPart = null, string? notes = null)
        => _conn.InsertAsync(new Exercise { Name = name.Trim(), BodyPart = bodyPart, Notes = notes });

    public Task DeleteAsync(int id)
        => _conn.DeleteAsync<Exercise>(id);
}
