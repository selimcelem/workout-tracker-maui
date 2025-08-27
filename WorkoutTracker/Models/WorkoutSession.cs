using SQLite;

namespace WorkoutTracker.Models;

public class WorkoutSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime DateUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsClosed { get; set; }   
}
