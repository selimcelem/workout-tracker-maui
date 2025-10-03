using SQLite;

namespace WorkoutTracker.Models;

public class WorkoutCategory
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed(Unique = true)] public string Name { get; set; } = "";
}
