using SQLite;

namespace WorkoutTracker.Models;

[Table("ExerciseCatalog")]
public class ExerciseCatalog
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }

    // Display name (unique)
    [Indexed(Unique = true), NotNull] public string Name { get; set; } = "";

    // Classification for progression rules
    // true = compound (bench, squat…), false = isolation (curl, raise…)
    public bool IsCompound { get; set; }

    // Optional: body part hint (e.g., "Chest", "Biceps")
    public string? BodyPart { get; set; }

    // Optional per-exercise default increment (kg), overrides global minimums
    public double? DefaultMinIncrementKg { get; set; }

    // Comma-separated aliases/synonyms (used for search)
    public string? AliasesCsv { get; set; }
}
