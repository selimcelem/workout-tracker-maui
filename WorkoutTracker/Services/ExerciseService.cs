using System.Linq;
using System.Collections.Generic;
using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public interface IExerciseService
{
    Task<List<Exercise>> GetAllAsync();

    // Keep original signature but return the created Exercise
    Task<Exercise> AddAsync(string name, string? bodyPart = null, string? notes = null);

    // Overload that can assign a category at creation time
    Task<Exercise> AddAsync(string name, int? categoryId, string? bodyPart = null, string? notes = null);
  
    Task RenameAsync(int id, string newName);
    Task DeleteAsync(int id);

    // (Optional, useful for category deletion/cleanup)
    Task ReassignCategoryAsync(int exerciseId, int? categoryId);
    Task UnassignAllInCategoryAsync(int categoryId);
}

public class ExerciseService : IExerciseService
{
    private readonly SQLiteAsyncConnection _conn;
    public ExerciseService(SQLiteAsyncConnection conn) => _conn = conn;

    public Task<List<Exercise>> GetAllAsync()
        => _conn.Table<Exercise>().OrderBy(e => e.Name).ToListAsync();

    // Old signature but forward the new overload
    public Task<Exercise> AddAsync(string name, string? bodyPart = null, string? notes = null)
        => AddAsync(name, categoryId: null, bodyPart: bodyPart, notes: notes);

    // New overload with category support
    public async Task<Exercise> AddAsync(string name, int? categoryId, string? bodyPart = null, string? notes = null)
    {
        var ex = new Exercise
        {
            Name = (name ?? "").Trim(),
            BodyPart = bodyPart?.Trim(),
            Notes = notes?.Trim(),
            CategoryId = categoryId
        };
        await _conn.InsertAsync(ex);
        return ex;
    }

    public Task RenameAsync(int id, string newName) =>
     _conn.ExecuteAsync("UPDATE Exercise SET Name = ? WHERE Id = ?", newName.Trim(), id);
    public Task DeleteAsync(int id)
        => _conn.DeleteAsync<Exercise>(id);

    // (Optional helpers for category CRUD)
    public Task ReassignCategoryAsync(int exerciseId, int? categoryId)
        => _conn.ExecuteAsync("UPDATE Exercise SET CategoryId = ? WHERE Id = ?", categoryId, exerciseId);

    public Task UnassignAllInCategoryAsync(int categoryId)
        => _conn.ExecuteAsync("UPDATE Exercise SET CategoryId = NULL WHERE CategoryId = ?", categoryId);
}
