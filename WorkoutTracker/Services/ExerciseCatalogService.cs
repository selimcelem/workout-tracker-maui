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

    // Broader, consolidated seed list
    public async Task SeedDefaultsAsync()
    {
        // Seed only if empty
        var count = await _conn.Table<ExerciseCatalogItem>().CountAsync();
        if (count > 0) return;

        var items = new List<ExerciseCatalogItem>
    {
        // -----------------------
        // SQUAT & KNEE-DOMINANT
        // -----------------------
        NI("Back Squat (Barbell)", "Legs", true, 2.5),
        NI("High-Bar Squat (Barbell)", "Legs", true, 2.5),
        NI("Low-Bar Squat (Barbell)", "Legs", true, 2.5),
        NI("Front Squat (Barbell)", "Legs", true, 2.5),
        NI("Box Squat (Barbell)", "Legs", true, 2.5),
        NI("Pause Squat (Barbell)", "Legs", true, 2.5),
        NI("Pin Squat (Barbell)", "Legs", true, 2.5),
        NI("Zercher Squat (Barbell)", "Legs", true, 2.5),
        NI("Safety Bar Squat", "Legs", true, 2.5),
        NI("Goblet Squat (Dumbbell)", "Legs", true, 2.5),
        NI("Dumbbell Squat", "Legs", true, 2.5),
        NI("Hack Squat (Machine)", "Legs", true, 2.5),
        NI("Smith Machine Squat", "Legs", true, 2.5),
        NI("Leg Press (45°)", "Legs", true, 5.0),
        NI("Single-Leg Press", "Legs", true, 2.5),
        NI("Walking Lunge (Dumbbells)", "Legs", true, 2.5),
        NI("Reverse Lunge (Barbell)", "Legs", true, 2.5),
        NI("Bulgarian Split Squat (DB)", "Legs", true, 2.5),
        NI("Split Squat (Barbell)", "Legs", true, 2.5),
        NI("Leg Extension", "Quads", false, 1.25),
        NI("Sissy Squat", "Quads", false, 1.0),

        // -----------------------
        // HIP HINGE & POSTERIOR
        // -----------------------
        NI("Deadlift (Barbell)", "Back", true, 2.5),
        NI("Sumo Deadlift (Barbell)", "Back", true, 2.5),
        NI("Trap Bar Deadlift", "Back", true, 2.5),
        NI("Romanian Deadlift (Barbell)", "Hamstrings", true, 2.5),
        NI("Stiff-Leg Deadlift (Barbell)", "Hamstrings", true, 2.5),
        NI("Romanian Deadlift (Dumbbells)", "Hamstrings", true, 2.5),
        NI("Good Morning (Barbell)", "Hamstrings", true, 2.5),
        NI("Back Extension (45°/GHD)", "Lower Back", true, 2.5),
        NI("Seated Leg Curl (Machine)", "Hamstrings", false, 1.25),
        NI("Lying Leg Curl (Machine)", "Hamstrings", false, 1.25),
        NI("Nordic Hamstring Curl", "Hamstrings", false, 0),
        NI("Hip Thrust (Barbell)", "Glutes", true, 2.5),
        NI("Glute Bridge (Barbell)", "Glutes", true, 2.5),
        NI("Single-Leg Hip Thrust", "Glutes", true, 2.5),
        NI("Cable Glute Kickback", "Glutes", false, 1.0),

        // -----------------------
        // HORIZONTAL PRESS / CHEST
        // -----------------------
        NI("Bench Press (Barbell)", "Chest", true, 2.5),
        NI("Close-Grip Bench Press (Barbell)", "Triceps", true, 2.5),
        NI("Paused Bench Press (Barbell)", "Chest", true, 2.5),
        NI("Incline Bench Press (Barbell)", "Chest", true, 2.5),
        NI("Decline Bench Press (Barbell)", "Chest", true, 2.5),
        // Newly requested DB presses
        NI("Dumbbell Bench Press", "Chest", true, 2.5),
        NI("Incline Dumbbell Bench Press", "Chest", true, 2.5),
        NI("Decline Dumbbell Bench Press", "Chest", true, 2.5),
        // Newly requested cable presses
        NI("Flat Cable Press", "Chest", true, 1.25),
        NI("Incline Cable Press", "Chest", true, 1.25),
        // Machines & bodyweight
        NI("Seated Chest Press (Machine)", "Chest", true, 2.5),
        NI("Hammer Strength Chest Press", "Chest", true, 2.5),
        NI("Smith Machine Bench Press", "Chest", true, 2.5),
        NI("Push-Up", "Chest", true, 0),
        NI("Weighted Push-Up", "Chest", true, 2.5),
        // Fly variations
        NI("Cable Fly", "Chest", false, 1.0),
        NI("Incline Cable Fly", "Chest", false, 1.0),
        NI("Decline Cable Fly", "Chest", false, 1.0),
        NI("Pec Deck", "Chest", false, 1.0),
        NI("Dumbbell Fly", "Chest", false, 1.0),
        NI("Standing Cable Crossover", "Chest", false, 1.0),
        NI("Dips (Chest Emphasis)", "Chest", true, 0),

        // -----------------------
        // VERTICAL PRESS / SHOULDERS
        // -----------------------
        NI("Overhead Press (Barbell)", "Shoulders", true, 2.5),
        NI("Push Press (Barbell)", "Shoulders", true, 2.5),
        NI("Seated OHP (Barbell)", "Shoulders", true, 2.5),
        NI("Strict Press (Dumbbells)", "Shoulders", true, 2.5),
        NI("Seated Shoulder Press (DB)", "Shoulders", true, 2.5),
        NI("Arnold Press (DB)", "Shoulders", true, 2.5),
        NI("Machine Shoulder Press", "Shoulders", true, 2.5),
        // Lateral & rear 
        NI("Lateral Raise (Dumbbell)", "Shoulders", false, 1.0),
        NI("Lateral Raise (Cable)", "Shoulders", false, 1.0),
        NI("Machine Lateral Raise", "Shoulders", false, 1.0),
        NI("Rear Delt Fly (Dumbbell)", "Rear Delts", false, 1.0),
        NI("Reverse Pec Deck", "Rear Delts", false, 1.0),
        NI("Face Pull (Cable)", "Rear Delts", false, 1.0),
        // FRONT RAISES
        NI("Front Raise (Dumbbell)", "Front Delts", false, 1.0),
        NI("Front Raise (Barbell)", "Front Delts", false, 1.25),
        NI("Front Raise (Cable)", "Front Delts", false, 1.0),
        NI("Plate Front Raise", "Front Delts", false, 1.0),
        NI("Seated Front Raise (DB)", "Front Delts", false, 1.0),
        // Extra rear-delt accessories
        NI("Reverse Fly (Cable)", "Rear Delts", false, 1.0),
        NI("Reverse Fly (Machine)", "Rear Delts", false, 1.0),
        NI("Rear Delt Row (DB)", "Rear Delts", true, 1.25),

        // -----------------------
        // HORIZONTAL PULL
        // -----------------------
        NI("Barbell Row (Bent-Over)", "Back", true, 2.5),
        NI("Pendlay Row (Barbell)", "Back", true, 2.5),
        NI("T-Bar Row", "Back", true, 2.5),
        NI("Seal Row (Barbell/DB)", "Back", true, 2.5),
        NI("Chest-Supported Row (Machine)", "Back", true, 2.5),
        NI("One-Arm Row (DB)", "Back", true, 2.5),
        NI("Cable Row (Seated)", "Back", true, 1.25),
        NI("Machine High Row", "Back", true, 2.5),
        NI("Meadow’s Row", "Back", true, 2.5),

        // -----------------------
        // VERTICAL PULL (incl. your missing pulldowns)
        // -----------------------
        NI("Pull-Up (Pronated)", "Lats", true, 0),
        NI("Chin-Up (Supinated)", "Lats", true, 0),
        NI("Neutral-Grip Pull-Up", "Lats", true, 0),
        NI("Weighted Pull-Up", "Lats", true, 2.5),
        NI("Lat Pulldown (Wide Grip)", "Lats", true, 1.25),
        NI("Lat Pulldown (Medium Grip)", "Lats", true, 1.25),
        NI("Lat Pulldown (Close Grip V-Bar)", "Lats", true, 1.25),
        NI("Neutral-Grip Pulldown", "Lats", true, 1.25),
        NI("Behind-the-Neck Pulldown", "Lats", true, 1.25),
        NI("Single-Arm Pulldown (Cable)", "Lats", false, 1.0),
        NI("Straight-Arm Pulldown (Cable)", "Lats", false, 1.0),

        // -----------------------
        // BICEPS
        // -----------------------
        NI("Barbell Curl", "Biceps", false, 1.25),
        NI("EZ-Bar Curl", "Biceps", false, 1.25),
        NI("Dumbbell Curl (Alternating)", "Biceps", false, 1.0),
        NI("Incline DB Curl", "Biceps", false, 1.0),
        NI("Hammer Curl", "Biceps", false, 1.0),
        NI("Bayesian Cable Curl", "Biceps", false, 1.0),
        NI("Cable Curl (EZ/Bar)", "Biceps", false, 1.0),
        NI("Preacher Curl (Machine)", "Biceps", false, 1.0),
        NI("Spider Curl", "Biceps", false, 1.0),
        NI("Concentration Curl", "Biceps", false, 1.0),

        // -----------------------
        // TRICEPS
        // -----------------------
        NI("Dip (Triceps Emphasis)", "Triceps", true, 0),
        NI("Skullcrusher (EZ-Bar)", "Triceps", false, 1.25),
        NI("Overhead Triceps Extension (DB)", "Triceps", false, 1.0),
        NI("Cable Pushdown (Rope)", "Triceps", false, 1.0),
        NI("Cable Pushdown (Bar/V)", "Triceps", false, 1.0),
        NI("JM Press", "Triceps", true, 1.25),
        NI("Reverse Grip Pressdown", "Triceps", false, 1.0),

        // -----------------------
        // CALVES
        // -----------------------
        NI("Standing Calf Raise (Machine)", "Calves", false, 1.25),
        NI("Seated Calf Raise (Machine)", "Calves", false, 1.25),
        NI("Donkey Calf Raise", "Calves", false, 1.25),
        NI("Leg Press Calf Raise", "Calves", false, 1.25),

        // -----------------------
        // CORE
        // -----------------------
        NI("Hanging Leg Raise", "Core", false, 0),
        NI("Toes to Bar", "Core", false, 0),
        NI("Cable Crunch", "Core", false, 1.0),
        NI("Weighted Plank", "Core", false, 2.5),
        NI("Ab Wheel Rollout", "Core", false, 0),
        NI("Pallof Press (Cable)", "Core", false, 1.0),
        NI("Russian Twist (Plate/DB)", "Core", false, 2.5),
        NI("Side Plank", "Core", false, 0),

        // -----------------------
        // OLYMPIC / POWER VARIATIONS
        // -----------------------
        NI("Clean Pull", "Full Body", true, 2.5),
        NI("Power Clean", "Full Body", true, 2.5),
        NI("Hang Clean", "Full Body", true, 2.5),
        NI("Snatch High Pull", "Full Body", true, 2.5),
        NI("Push Jerk", "Full Body", true, 2.5),

        // -----------------------
        // KB / SLED / CARRIES
        // -----------------------
        NI("Kettlebell Swing (2-Hand)", "Posterior Chain", true, 2.5),
        NI("Kettlebell Swing (1-Hand)", "Posterior Chain", true, 2.5),
        NI("Goblet Carry", "Core", true, 2.5),
        NI("Farmer’s Carry (DB)", "Forearms", true, 2.5),
        NI("Suitcase Carry (DB)", "Core", true, 2.5),
        NI("Sled Push", "Legs", true, 5.0),
        NI("Sled Pull (Backward)", "Quads", true, 5.0),

        // -----------------------
        // NECK / TRAPS / FOREARMS (incl. your missing shrugs)
        // -----------------------
        NI("Barbell Shrug (Front)", "Traps", false, 2.5),
        NI("Barbell Shrug (Behind-the-Back)", "Traps", false, 2.5),
        NI("Dumbbell Shrug", "Traps", false, 2.5),
        NI("Smith Machine Shrug", "Traps", false, 2.5),
        NI("Cable Shrug (Low Pulley)", "Traps", false, 1.25),
        NI("Farmer’s Hold (Heavy Dumbbells)", "Traps", true, 2.5),
        NI("Behind-the-Back Shrug (BB)", "Traps", false, 2.5),
        NI("Wrist Curl (DB/Bar)", "Forearms", false, 1.0),
        NI("Reverse Wrist Curl (DB/Bar)", "Forearms", false, 1.0),

        // -----------------------
        // REHAB / ACCESSORY (light)
        // -----------------------
        NI("External Rotation (Cable)", "Rotator Cuff", false, 0.5),
        NI("Internal Rotation (Cable)", "Rotator Cuff", false, 0.5),
        NI("Scapular Pull-Up", "Back", false, 0),
        NI("Banded Face Pull", "Rear Delts", false, 0.5),
        NI("Banded Pull-Apart", "Rear Delts", false, 0.5),
    };

        await _conn.InsertAllAsync(items);

        // Local helper to keep entries terse
        static ExerciseCatalogItem NI(string name, string bodyPart, bool compound, double incKg) =>
            new ExerciseCatalogItem
            {
                Name = name,
                BodyPart = bodyPart,
                IsCompound = compound,
                DefaultIncrementKg = incKg
            };
    }

    // Search anywhere in the name, rank by match position
    public async Task<IReadOnlyList<ExerciseCatalogItem>> SearchAsync(string fragment, int limit = 15)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return Array.Empty<ExerciseCatalogItem>();
        fragment = fragment.Trim();

        // Rank results with earlier matches first, then alphabetically
        var sql = $@"
SELECT * FROM ExerciseCatalogItem
WHERE Name LIKE ? ESCAPE '\'
COLLATE NOCASE
ORDER BY INSTR(LOWER(Name), LOWER(?)), Name
LIMIT {limit}";

        // wrap fragment with % for contains search
        var like = $"%{EscapeLike(fragment)}%";
        return await _conn.QueryAsync<ExerciseCatalogItem>(sql, like, fragment);

        static string EscapeLike(string s) =>
            s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
    }
}
