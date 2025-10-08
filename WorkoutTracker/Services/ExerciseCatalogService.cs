using SQLite;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

public sealed class ExerciseCatalogService : IExerciseCatalogService
{
    private readonly SQLiteAsyncConnection _conn;
    public ExerciseCatalogService(SQLiteAsyncConnection conn) => _conn = conn;

    public async Task EnsureCreatedAsync()
    {
        await _conn.CreateTableAsync<ExerciseCatalogItem>();
        await _conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_ExerciseCatalogItem_Name ON ExerciseCatalogItem(Name)");
    }

    public async Task SeedDefaultsAsync()
    {
        var count = await _conn.Table<ExerciseCatalogItem>().CountAsync();
        if (count > 0) return;

        var items = new[]
    {
        // --- Squat / Lower ---
        new ExerciseCatalogItem { Name = "Back Squat (Barbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Front Squat (Barbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "High-Bar Squat (Barbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Low-Bar Squat (Barbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Box Squat (Barbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Goblet Squat (Dumbbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Dumbbell Squat", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Hack Squat (Machine)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Smith Machine Squat", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Deadlift / Hip hinge ---
        new ExerciseCatalogItem { Name = "Deadlift (Barbell)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Sumo Deadlift (Barbell)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Romanian Deadlift (Barbell)", BodyPart = "Hamstrings", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Romanian Deadlift (Dumbbells)", BodyPart = "Hamstrings", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Trap Bar Deadlift", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Stiff-Leg Deadlift (Barbell)", BodyPart = "Hamstrings", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Good Morning (Barbell)", BodyPart = "Hamstrings", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Bench / Chest press ---
        new ExerciseCatalogItem { Name = "Bench Press (Barbell)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Bench Press (Dumbbells)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Incline Bench Press (Barbell)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Incline Bench Press (Dumbbells)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Decline Bench Press (Barbell)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Decline Bench Press (Dumbbells)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Machine Chest Press", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Push-Up", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 0 },

        // --- Overhead press / Shoulders ---
        new ExerciseCatalogItem { Name = "Overhead Press (Barbell)", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Seated Overhead Press (Barbell)", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Shoulder Press (Dumbbells)", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Arnold Press (Dumbbells)", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Machine Shoulder Press", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Push Press (Barbell)", BodyPart = "Shoulders", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Row / Horizontal pull ---
        new ExerciseCatalogItem { Name = "Bent-Over Row (Barbell)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Pendlay Row (Barbell)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "One-Arm Row (Dumbbell)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Chest-Supported Row (Dumbbells)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "T-Bar Row", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Seated Cable Row", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Machine Row", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Vertical pull ---
        new ExerciseCatalogItem { Name = "Pull-Up", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 0 },
        new ExerciseCatalogItem { Name = "Chin-Up", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 0 },
        new ExerciseCatalogItem { Name = "Lat Pulldown (Wide Grip)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Lat Pulldown (Close/Neutral)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Quads / Hamstrings accessories ---
        new ExerciseCatalogItem { Name = "Leg Press (45°)", BodyPart = "Quads", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Leg Extension (Machine)", BodyPart = "Quads", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Leg Curl (Seated)", BodyPart = "Hamstrings", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Leg Curl (Lying)", BodyPart = "Hamstrings", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Nordic Curl", BodyPart = "Hamstrings", IsCompound = false, DefaultIncrementKg = 0 },
        new ExerciseCatalogItem { Name = "Hip Thrust (Barbell)", BodyPart = "Glutes", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Glute Bridge (Barbell)", BodyPart = "Glutes", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Bulgarian Split Squat (Dumbbells)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Walking Lunge (Dumbbells)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Reverse Lunge (Barbell)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Step-Up (Dumbbells)", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Chest accessories ---
        new ExerciseCatalogItem { Name = "Chest Fly (Dumbbells)", BodyPart = "Chest", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Cable Fly (High-to-Low)", BodyPart = "Chest", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Cable Fly (Low-to-High)", BodyPart = "Chest", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Pec Deck (Machine)", BodyPart = "Chest", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Dips (Chest Lean)", BodyPart = "Chest", IsCompound = true, DefaultIncrementKg = 0 },

        // --- Shoulders accessories ---
        new ExerciseCatalogItem { Name = "Lateral Raise (Dumbbells)", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Cable Lateral Raise", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Machine Lateral Raise", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Rear Delt Fly (Dumbbells)", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Reverse Pec Deck", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Front Raise (Dumbbells)", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Face Pull (Cable)", BodyPart = "Shoulders", IsCompound = false, DefaultIncrementKg = 1.0 },

        // --- Back accessories ---
        new ExerciseCatalogItem { Name = "Straight-Arm Pulldown (Cable)", BodyPart = "Back", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Meadows Row", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Seal Row (Barbell)", BodyPart = "Back", IsCompound = true, DefaultIncrementKg = 2.5 },

        // --- Arms: biceps ---
        new ExerciseCatalogItem { Name = "Biceps Curl (Dumbbells)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Biceps Curl (Barbell)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "EZ-Bar Curl", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Hammer Curl (Dumbbells)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Incline Dumbbell Curl", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Preacher Curl (EZ-Bar)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Cable Curl", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Concentration Curl", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.0 },

        // --- Arms: triceps ---
        new ExerciseCatalogItem { Name = "Close-Grip Bench Press", BodyPart = "Arms", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Skull Crusher (EZ-Bar)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Overhead Triceps Extension (Dumbbell)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Triceps Pushdown (Cable)", BodyPart = "Arms", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Dip (Triceps Emphasis)", BodyPart = "Arms", IsCompound = true, DefaultIncrementKg = 0 },

        // --- Calves ---
        new ExerciseCatalogItem { Name = "Standing Calf Raise (Machine)", BodyPart = "Calves", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Seated Calf Raise (Machine)", BodyPart = "Calves", IsCompound = false, DefaultIncrementKg = 1.25 },
        new ExerciseCatalogItem { Name = "Donkey Calf Raise", BodyPart = "Calves", IsCompound = false, DefaultIncrementKg = 1.25 },

        // --- Core ---
        new ExerciseCatalogItem { Name = "Hanging Leg Raise", BodyPart = "Core", IsCompound = false, DefaultIncrementKg = 0 },
        new ExerciseCatalogItem { Name = "Cable Crunch", BodyPart = "Core", IsCompound = false, DefaultIncrementKg = 1.0 },
        new ExerciseCatalogItem { Name = "Ab Wheel Rollout", BodyPart = "Core", IsCompound = false, DefaultIncrementKg = 0 },
        new ExerciseCatalogItem { Name = "Weighted Plank", BodyPart = "Core", IsCompound = false, DefaultIncrementKg = 2.5 },

        // --- Misc / Accessories ---
        new ExerciseCatalogItem { Name = "Farmer's Walk (Dumbbells)", BodyPart = "Full Body", IsCompound = true, DefaultIncrementKg = 2.5 },
        new ExerciseCatalogItem { Name = "Sled Push", BodyPart = "Legs", IsCompound = true, DefaultIncrementKg = 5.0 },
        new ExerciseCatalogItem { Name = "Sled Pull (Backward)", BodyPart = "Quads", IsCompound = true, DefaultIncrementKg = 5.0 },
    };


        await _conn.InsertAllAsync(items);
    }

    public async Task<IReadOnlyList<ExerciseCatalogItem>> SearchAsync(string fragment, int limit = 15)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return Array.Empty<ExerciseCatalogItem>();
        fragment = fragment.Trim();

        var sql = $"SELECT * FROM ExerciseCatalogItem WHERE Name LIKE ? COLLATE NOCASE ORDER BY Name LIMIT {limit}";
        return await _conn.QueryAsync<ExerciseCatalogItem>(sql, $"{fragment}%");
    }
}
