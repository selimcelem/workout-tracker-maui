using SQLite;

namespace WorkoutTracker.Models;

public class SetEntry
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int SessionId { get; set; }
    public int ExerciseId { get; set; }
    public int SetNumber { get; set; }
    public int Reps { get; set; }
    public double Weight { get; set; }
    public double? Rpe { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
