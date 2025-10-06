namespace WorkoutTracker.Models;

public class ExerciseCatalogEntry
{
    [SQLite.PrimaryKey, SQLite.AutoIncrement]
    public int Id { get; set; }

    [SQLite.Unique, SQLite.NotNull]
    public string Name { get; set; } = "";
}
