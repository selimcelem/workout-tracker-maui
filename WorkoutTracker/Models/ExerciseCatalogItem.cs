namespace WorkoutTracker.Models;

public class ExerciseCatalogItem
{
    [SQLite.PrimaryKey, SQLite.AutoIncrement]
    public int Id { get; set; }

    [SQLite.Indexed, SQLite.NotNull]
    public string Name { get; set; } = "";

    public string? BodyPart { get; set; }
    public bool IsCompound { get; set; }

    // Optional: smaller increments for isolation lifts (future use)
    public double? DefaultIncrementKg { get; set; }
}
