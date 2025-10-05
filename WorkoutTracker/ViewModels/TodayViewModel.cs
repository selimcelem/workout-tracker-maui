using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using System.Linq;
using System.Globalization;


namespace WorkoutTracker.ViewModels;

public partial class TodayViewModel : ObservableObject
{
    private readonly ISessionService _sessions;
    private readonly ISetService _sets;
    private readonly IExerciseService _exercises;
    private readonly ICategoryService _categories;

    public TodayViewModel(ISessionService sessions,
        ISetService sets,
        IExerciseService exercises,
        ICategoryService categories)
    {
        _sessions = sessions;
        _sets = sets;
        _exercises = exercises;
        _categories = categories;
    }

    // Track the last prefilled recommendation to judge success/failure
    private int? _lastRecommendedReps;
    private double? _lastRecommendedWeight;
    private static double RoundToStep(double value, double stepKg)
    => Math.Round(value / stepKg, MidpointRounding.AwayFromZero) * stepKg;
    private const int TargetRepsLow = 8;
    private const int TargetRepsHigh = 12;
    private const double TargetRir = 2.0;   // ≈ RPE 8
    private const double MaxStepUpPct = 0.06;  // +6% cap
    private const double MaxStepDnPct = 0.08;  // -8% cap
    private const double FatigueDropPct = 0.07;
    private const double RoundStepKg = 2.5;
    private const double MinDeltaKg = 5.0;
    // Track anchors per exercise for the current session
    private int? _sessionIdAnchor;
    private readonly Dictionary<int, Performance> _sessionAnchors = new();



    private static double EnforceMinDelta(double newWeight, double lastWeight, double minDeltaKg)
    {
        var delta = newWeight - lastWeight;
        if (Math.Abs(delta) < minDeltaKg)
            newWeight = lastWeight + Math.Sign(delta == 0 ? 1 : delta) * minDeltaKg;
        return newWeight;
    }


    // --- UI state ---
    [ObservableProperty] private WorkoutSession? currentSession;

    [ObservableProperty] private ObservableCollection<WorkoutCategory> categoryOptions = new();
    [ObservableProperty] private WorkoutCategory? selectedCategory;

    [ObservableProperty] private ObservableCollection<Exercise> exerciseOptions = new();
    [ObservableProperty] private Exercise? selectedExercise;

    // react to category change > filter exercises

    partial void OnSelectedCategoryChanged(WorkoutCategory? value)
    {
        _= FilterExercisesForCategoryAsync(value);
    }
    partial void OnSelectedExerciseChanged(Exercise? value)
    {
        _ = RecommendFromLastSessionAsync(value);
    }
    [ObservableProperty] private bool hasActiveSession;

    // Inputs
    // Reps/Weight as strings so placeholders show and Entry.Text never throws
    [ObservableProperty] private string reps = string.Empty;
    [ObservableProperty] private string weightText = string.Empty;

    [ObservableProperty] private bool hasExercisesInCategory;

    private async Task FilterExercisesForCategoryAsync(WorkoutCategory? cat)
    {
        var all = await _exercises.GetAllAsync();

        List<Exercise> list;
        if (cat == null)
        {
            list = all.OrderBy(e => e.Name).ToList();
        }
        else
        {
            list = all.Where(e => e.CategoryId == cat.Id)
                      .OrderBy(e => e.Name)
                      .ToList();
        }

        ExerciseOptions = new ObservableCollection<Exercise>(list);
        HasExercisesInCategory = ExerciseOptions.Count > 0;

        SelectedExercise = ExerciseOptions.FirstOrDefault();
    }


    // RPE (0–10) as number + description for Picker popup
    public sealed class RpeOption
    {
        public string Display { get; }      // e.g., "0 — No effort at all"
        public double? Value { get; }       // e.g., 0..10, or null
        public RpeOption(string display, double? value) { Display = display; Value = value; }
    }

    public ObservableCollection<RpeOption> RpeOptions { get; } = new()
{
    new("0 — No effort at all", 0),
    new("1–2 — Very easy", 1.5),
    new("3–4 — Easy / somewhat hard", 3.5),
    new("5–6 — Hard", 5.5),
    new("7–8 — Very hard", 7.5),
    new("9 — Near max", 9),
    new("10 — Maximal effort", 10),
};

    [ObservableProperty] private RpeOption? selectedRpe;


    // Rendered list for "Today"
    [ObservableProperty] private ObservableCollection<SetDisplay> todaysSets = new();

    // Load current session + exercises + existing sets
    [RelayCommand]
    public async Task Load()
    {
        // Ensure defaults exist (method should be idempotent)
        await _categories.SeedDefaultsAsync();

        // 1) Load categories
        var cats = await _categories.GetAllAsync();
        CategoryOptions = new ObservableCollection<WorkoutCategory>(cats);
        if (SelectedCategory == null)
            SelectedCategory = CategoryOptions.FirstOrDefault();

        // 2) Load exercises filtered by category
        await FilterExercisesForCategoryAsync(SelectedCategory);

        // 3) Load session + sets (unchanged)
        CurrentSession = await _sessions.GetOpenSessionAsync();

        // If the open session changed since last time, clear anchors
        var newId = CurrentSession?.Id;
        if (newId != _sessionIdAnchor)
        {
            _sessionIdAnchor = newId;
            _sessionAnchors.Clear();
        }

        if (CurrentSession != null)
        {
            var nameById = ExerciseOptions.ToDictionary(e => e.Id, e => e.Name);
            var raw = await _sets.GetBySessionAsync(CurrentSession.Id);

            TodaysSets = new ObservableCollection<SetDisplay>(
                raw.Select(s => new SetDisplay
                {
                    Id = s.Id,
                    TimestampUtc = s.TimestampUtc,
                    ExerciseName = nameById.TryGetValue(s.ExerciseId, out var n) ? n : $"#{s.ExerciseId}",
                    SetNumber = s.SetNumber,
                    Reps = s.Reps,
                    Weight = s.Weight,
                    Rpe = s.Rpe
                }));

            HasActiveSession = true;
        }
        else
        {
            TodaysSets.Clear();
            HasActiveSession = false;
        }
    }

    [RelayCommand]
    private async Task AddExercise()
    {
        if (SelectedCategory == null)
        {
            await Shell.Current.DisplayAlert("Pick a category", "Select a category first.", "OK");
            return;
        }

        // Let user choose: reuse existing or create new
        var flow = await Shell.Current.DisplayActionSheet(
            "Add exercise", "Cancel", null, "Pick existing", "Create new");
        if (string.IsNullOrEmpty(flow) || flow == "Cancel") return;

        if (flow == "Pick existing")
        {
            // Prefer uncategorized first
            var pool = await _exercises.GetUncategorizedAsync();

            // Fallback: allow any exercise not already in this category
            if (pool.Count == 0)
            {
                var all = await _exercises.GetAllAsync();
                pool = all.Where(e => e.CategoryId != SelectedCategory.Id)
                          .OrderBy(e => e.Name)
                          .ToList();
            }

            if (pool.Count == 0)
            {
                await Shell.Current.DisplayAlert("Nothing to add", "No available exercises to pick. Create a new one instead.", "OK");
                return;
            }

            var names = pool.Select(e => e.Name).ToArray();
            var chosenName = await Shell.Current.DisplayActionSheet(
                $"Add to {SelectedCategory.Name}", "Cancel", null, names);
            if (string.IsNullOrEmpty(chosenName) || chosenName == "Cancel") return;

            var picked = pool.First(e => e.Name == chosenName);

            // Assign the picked exercise to the selected category
            await _exercises.ReassignCategoryAsync(picked.Id, SelectedCategory.Id);

            // Refresh + select
            await FilterExercisesForCategoryAsync(SelectedCategory);
            SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == picked.Id);
            return;
        }

        // flow == "Create new"
        var name = await Shell.Current.DisplayPromptAsync(
            "New Exercise", $"Add to: {SelectedCategory.Name}", "Add", "Cancel", "Exercise name");
        if (string.IsNullOrWhiteSpace(name)) return;

        // If a same-named exercise already exists, reuse it instead of crashing on UNIQUE(Name)
        var existing = await _exercises.GetByNameAsync(name);
        if (existing != null)
        {
            await _exercises.ReassignCategoryAsync(existing.Id, SelectedCategory.Id);
            await FilterExercisesForCategoryAsync(SelectedCategory);
            SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == existing.Id);
            return;
        }

        // Otherwise create it directly under this category
        var added = await _exercises.AddAsync(name.Trim(), SelectedCategory.Id);
        await FilterExercisesForCategoryAsync(SelectedCategory);
        SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == added.Id);
    }


    [RelayCommand]
    public async Task StartSession()
    {
        CurrentSession = await _sessions.StartSessionAsync();
        _sessionIdAnchor = CurrentSession?.Id;
        _sessionAnchors.Clear();
        TodaysSets.Clear();
        HasActiveSession = true;
    }

    [RelayCommand]
    public async Task AddSet()
    {
        // 1) Require an open session
        if (CurrentSession == null)
        {
            await Shell.Current.DisplayAlert(
                "Start a session",
                "You need to start a session before adding sets.",
                "OK");
            return;
        }

        // 2) Validate input
        if (SelectedExercise == null)
        {
            await Shell.Current.DisplayAlert("Missing exercise", "Please select an exercise.", "OK");
            return;
        }

        if (!int.TryParse(Reps, out var repsValue) || repsValue <= 0)
        {
            await Shell.Current.DisplayAlert("Invalid reps", "Enter reps greater than 0.", "OK");
            return;
        }

        if (!double.TryParse(WeightText, out var weight) || weight < 0)
        {
            await Shell.Current.DisplayAlert("Invalid weight", "Enter a non-negative number.", "OK");
            return;
        }

        // RPE is optional (nullable). Persist as double? to match model.
        double? rpeToSave = SelectedRpe?.Value;

        // 3) Save (SetService assigns correct next SetNumber per session+exercise)
        var entry = await _sets.AddAsync(
            sessionId: CurrentSession.Id,
            exerciseId: SelectedExercise.Id,
            reps: repsValue,
            weight: weight,
            rpe: rpeToSave
        );

        // 4) Update UI list
        TodaysSets.Add(new SetDisplay
        {
            Id = entry.Id,
            TimestampUtc = entry.TimestampUtc,
            ExerciseName = SelectedExercise.Name,
            SetNumber = entry.SetNumber,
            Reps = entry.Reps,
            Weight = entry.Weight,
            Rpe = entry.Rpe
        });

        // 5) Small UX reset (keep weight for convenience)
        Reps = string.Empty;
        // keep WeightText as-is for faster entry
        SelectedRpe = null; // optional

        // After adding to TodaysSets and resetting fields:
        await RecommendFromLastSessionAsync(SelectedExercise);
    }

    [RelayCommand]
    private async Task EndSession()
    {
        if (CurrentSession == null) return;

        await _sessions.EndSessionAsync(CurrentSession.Id);

        CurrentSession = null;
        HasActiveSession = false;
        TodaysSets.Clear();
    }

    // swipe-to-delete
    [RelayCommand]
    private async Task DeleteSet(SetDisplay? item)
    {
        if (item == null) return;

        var ok = await Shell.Current.DisplayAlert(
            "Delete set",
            $"Delete {item.ExerciseName} · Set {item.SetNumber} · {item.Reps} reps × {item.Weight:0.##} kg?",
            "Delete", "Cancel");

        if (!ok) return;

        await _sets.DeleteAsync(item.Id);
        TodaysSets.Remove(item);
    }

    [RelayCommand]
    private async Task AddCategory()
    {
        var name = await Shell.Current.DisplayPromptAsync("New Category", "Name", "Add", "Cancel", "e.g. Push Day");
        if (string.IsNullOrWhiteSpace(name)) return;

        WorkoutCategory added;
        try
        {
            added = await _categories.AddAsync(name.Trim());
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            return;
        }

        // Refresh list and select
        var cats = await _categories.GetAllAsync();
        CategoryOptions = new ObservableCollection<WorkoutCategory>(cats);
        SelectedCategory = CategoryOptions.FirstOrDefault(c => c.Id == added.Id);
    }

    [RelayCommand]
    private async Task RenameCategory()
    {
        if (SelectedCategory == null) return;

        var newName = await Shell.Current.DisplayPromptAsync("Rename Category", "New name", "Save", "Cancel", initialValue: SelectedCategory.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            await _categories.RenameAsync(SelectedCategory.Id, newName.Trim());
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            return;
        }

        // Refresh list and keep selection on this category
        var id = SelectedCategory.Id;
        var cats = await _categories.GetAllAsync();
        CategoryOptions = new ObservableCollection<WorkoutCategory>(cats);
        SelectedCategory = CategoryOptions.FirstOrDefault(c => c.Id == id);
    }

    [RelayCommand]
    private async Task DeleteCategory()
    {
        if (SelectedCategory == null) return;

        var confirm = await Shell.Current.DisplayAlert(
            "Delete Category",
            $"Delete '{SelectedCategory.Name}'? Exercises in it will remain available (uncategorized).",
            "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _exercises.UnassignAllInCategoryAsync(SelectedCategory.Id); // detach exercises
            await _categories.DeleteAsync(SelectedCategory.Id);                // remove category
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            return;
        }

        // Refresh list + selection
        var cats = await _categories.GetAllAsync();
        CategoryOptions = new ObservableCollection<WorkoutCategory>(cats);
        SelectedCategory = CategoryOptions.FirstOrDefault(); // may be null if none remain
        await FilterExercisesForCategoryAsync(SelectedCategory);
    }
    [RelayCommand]
    private async Task RenameExercise()
    {
        if (SelectedExercise == null) return;

        var newName = await Shell.Current.DisplayPromptAsync(
            "Rename Exercise", "New name", "Save", "Cancel", initialValue: SelectedExercise.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            await _exercises.RenameAsync(SelectedExercise.Id, newName.Trim());
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            return;
        }

        // Refresh current category’s list and keep selection
        var id = SelectedExercise.Id;
        await FilterExercisesForCategoryAsync(SelectedCategory);
        SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == id);
    }

    [RelayCommand]
    private async Task MoveExercise()
    {
        if (SelectedExercise == null) return;

        var currentExerciseId = SelectedExercise.Id;

        // Pick a category to move to
        var cats = CategoryOptions?.ToList() ?? new();
        if (cats.Count == 0)
        {
            await Shell.Current.DisplayAlert("No categories", "Create a category first.", "OK");
            return;
        }

        // Simple picker via DisplayActionSheet
        var names = cats.Select(c => c.Name).ToArray();
        var chosen = await Shell.Current.DisplayActionSheet("Move to category", "Cancel", null, names);
        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

        var target = cats.First(c => c.Name == chosen);

        try
        {
            await _exercises.ReassignCategoryAsync(SelectedExercise.Id, target.Id);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            return;
        }

        // Refresh lists: show target category and the moved exercise
        SelectedCategory = target;
        await FilterExercisesForCategoryAsync(SelectedCategory);
        SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == SelectedExercise.Id);
    }



    public class SetDisplay
    {
        public int Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string ExerciseName { get; set; } = "";
        public int SetNumber { get; set; }
        public int Reps { get; set; }
        public double Weight { get; set; }
        public double? Rpe { get; set; }
    }

    // Smarter recommendation logic:
    private async Task RecommendFromLastSessionAsync(Exercise? exercise)
    {
        if (exercise == null) return;

        IReadOnlyList<SetEntry> history;
        try
        {
            history = await _sets.GetLastSessionSetsForExerciseAsync(exercise.Id);
        }
        catch
        {
            return;
        }

        // We'll assign to these in either branch (avoid redeclaring twice)
        double chosenWeight;
        int chosenReps;

        // Latest set for this exercise today (if any)
        var lastToday = TodaysSets
            .Where(s => string.Equals(s.ExerciseName, exercise.Name, StringComparison.Ordinal))
            .OrderByDescending(s => s.SetNumber)
            .FirstOrDefault();

        // If no history and nothing today -> conservative starting point (unless user already typed a weight)
        if ((history == null || history.Count == 0) && lastToday == null)
        {
            chosenWeight = (double.TryParse(WeightText, out var w) && w > 0) ? w : 20.0;
            chosenReps = Math.Max(TargetRepsLow, 8);

            chosenWeight = RoundToStep(chosenWeight, RoundStepKg);
            Reps = chosenReps.ToString();
            WeightText = chosenWeight.ToString("0.##", CultureInfo.InvariantCulture);
            _lastRecommendedReps = chosenReps;
            _lastRecommendedWeight = chosenWeight;
            return;
        }

        // Build a small recent snapshot
        var recent = (history ?? Array.Empty<SetEntry>())
            .OrderByDescending(s => s.TimestampUtc)
            .Take(6)
            .ToList();

        // Choose an anchor performance:
        // - Within session: if we have already done a set today, anchor = that
        // - At the start of a session for this exercise: use (or create) a baseline anchor
        Performance anchor;
        if (lastToday != null)
        {
            anchor = new Performance
            {
                Weight = lastToday.Weight,
                Reps = lastToday.Reps,
                Rpe = lastToday.Rpe
            };
        }
        else
        {
            // No sets today for this exercise → session start for this exercise
            if (!_sessionAnchors.TryGetValue(exercise.Id, out anchor))
            {
                // Build a baseline from recent history
                var fromHistory = (history ?? Array.Empty<SetEntry>()).ToList();
                if (fromHistory.Count == 0)
                {
                    // No history at all: fall back to a conservative default
                    // We'll never reach here because of the earlier check
                    anchor = new Performance { Weight = 20.0, Reps = 8, Rpe = 8.0 };
                }
                else
                {
                    anchor = PickBaselineFromHistory(fromHistory);
                }
                // Cache for this session
                _sessionAnchors[exercise.Id] = anchor;
            }
        }

        // Estimate current e1RM from anchor
        var anchorE1 = EstimateE1Rm(anchor.Weight, anchor.Reps, anchor.Rpe);

        // Aim for mid-range reps at target RIR
        int targetReps = (TargetRepsLow + TargetRepsHigh) / 2; // e.g., 10
        double inferredRir = InferRirFromReps(anchor.Reps, targetReps, anchor.Rpe);

        // Predict weight for that goal
        double rawSuggested = WeightForRepsAtRir(anchorE1, targetReps, TargetRir);

        // Directional bias: if easier than desired (inferredRir > TargetRir) → increase;
        // if harder than desired (inferredRir < TargetRir) → decrease.
        var bias = Math.Clamp((inferredRir - TargetRir) * 0.02, -MaxStepDnPct, MaxStepUpPct);
        rawSuggested *= (1.0 + bias);

        // Back-off if last set was very hard
        if ((anchor.Rpe ?? 8.0) >= 9.0)
            rawSuggested *= (1.0 - FatigueDropPct);

        // Enforce deltas vs the last set today and plate rounding
        chosenWeight = rawSuggested;
        if (lastToday != null)
            chosenWeight = EnforceMinDelta(chosenWeight, lastToday.Weight, MinDeltaKg);

        chosenWeight = RoundToStep(chosenWeight, RoundStepKg);

        // Predict reps at the rounded weight so UI is consistent
        int predictedReps = PredictRepsAtWeight(anchorE1, chosenWeight, TargetRir);
        chosenReps = Math.Clamp(predictedReps, TargetRepsLow, TargetRepsHigh);

        // Fill UI
        Reps = chosenReps.ToString();
        WeightText = chosenWeight.ToString("0.##", CultureInfo.InvariantCulture);
        _lastRecommendedReps = chosenReps;
        _lastRecommendedWeight = chosenWeight;
    }

    // Helpers
    private sealed class Performance
{
    public double Weight { get; set; }
    public int Reps { get; set; }
    public double? Rpe { get; set; } // null if user didn’t give it
}

// Estimate e1RM using Epley, with RIR adjustment if RPE is known
    private static double EstimateE1Rm(double weight, int reps, double? rpe)
    {
        reps = Math.Max(1, reps);
        // Base (Epley)
        double e1 = weight * (1.0 + reps / 30.0);

        if (rpe.HasValue)
        {
            var rir = Math.Clamp(10.0 - rpe.Value, 0.0, 5.0);
            // Convert reps@RIR to estimated %1RM and adjust e1 accordingly
            var pct = Pct1RmFromRepsAndRir(reps, rir);
            if (pct > 0.20 && pct < 1.20)
            {
                // e1RM ≈ weight / pct
                e1 = weight / pct;
            }
        }
        return e1;
    }

    // When RPE not provided, infer roughly from distance to the target reps
    private static double InferRirFromReps(int achievedReps, int targetReps, double? rpe)
    {
        if (rpe.HasValue) return Math.Clamp(10.0 - rpe.Value, 0.0, 5.0);
        // Very rough: each rep below target = -1 RIR (i.e., it was harder), above target = +1 RIR
        int diff = targetReps - achievedReps; // positive if we did fewer than target (harder)
        double inferred = 2.0 + (-diff); // centered near RIR 2
        return Math.Clamp(inferred, 0.0, 5.0);
    }

    // Predict weight that should yield `reps` at `rir`, given e1RM
    private static double WeightForRepsAtRir(double e1rm, int reps, double rir)
    {
        double pct = Pct1RmFromRepsAndRir(reps, rir);
        return Math.Max(0.0, e1rm * pct);
    }

    // Predict reps achievable at a given weight, targeting a certain RIR, given e1RM
    private static int PredictRepsAtWeight(double e1rm, double weight, double rir)
    {
        // brute force search a small range 3..15 reps and pick closest
        int bestReps = 8;
        double bestErr = double.MaxValue;
        for (int r = 3; r <= 20; r++)
        {
            double pct = Pct1RmFromRepsAndRir(r, rir);
            double predictedW = e1rm * pct;
            double err = Math.Abs(predictedW - weight);
            if (err < bestErr) { bestErr = err; bestReps = r; }
        }
        return bestReps;
    }

    // Simple RIR ➜ %1RM model (piecewise linear around common charts)
    private static double Pct1RmFromRepsAndRir(int reps, double rir)
    {
        reps = Math.Clamp(reps, 1, 20);
        rir = Math.Clamp(rir, 0.0, 5.0);

        // At RIR 0 (RPE 10) common table (approx):
        // 1:100%, 3:93%, 5:87%, 8:80%, 10:75%, 12:70%, 15:65%, 20:55%
        double pctAtRir0 = reps switch
        {
            <= 1 => 1.00,
            2 => 0.96,
            3 => 0.93,
            4 => 0.90,
            5 => 0.87,
            6 => 0.85,
            7 => 0.82,
            8 => 0.80,
            9 => 0.77,
            10 => 0.75,
            11 => 0.72,
            12 => 0.70,
            13 => 0.68,
            14 => 0.66,
            15 => 0.65,
            <= 17 => 0.60,
            _ => 0.55
        };

        // Each +1 RIR ≈ -2.5% intensity (very rough but serviceable)
        double pct = pctAtRir0 * (1.0 - 0.025 * rir);
        return Math.Clamp(pct, 0.30, 1.10);
    }
    private static Performance PickBaselineFromHistory(IReadOnlyList<SetEntry> history)
    {
        // Prefer "working" sets (RPE 7–9), otherwise fallback to the heaviest recent set
        var working = history
            .Where(s => s.Rpe.HasValue && s.Rpe.Value >= 7.0 && s.Rpe.Value <= 9.0)
            .OrderByDescending(s => s.TimestampUtc)
            .ToList();

        SetEntry seed;
        if (working.Count > 0)
        {
            // pick the heaviest among working (tie-break by latest)
            seed = working
                .OrderByDescending(s => s.Weight)
                .ThenByDescending(s => s.TimestampUtc)
                .First();
        }
        else
        {
            seed = history
                .OrderByDescending(s => s.Weight)
                .ThenByDescending(s => s.TimestampUtc)
                .First();
        }

        return new Performance { Weight = seed.Weight, Reps = seed.Reps, Rpe = seed.Rpe };
    }
}

