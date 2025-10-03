using SQLite;

namespace WorkoutTracker.Models;

public class Exercise
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Unique, NotNull] public string Name { get; set; } = "";
    public string? BodyPart { get; set; }
    public string? Notes { get; set; }

    // Optional link to a category
    public int? CategoryId { get; set; }
}
