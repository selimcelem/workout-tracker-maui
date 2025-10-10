using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class TodayViewModel : ObservableObject
{
    private readonly ISessionService _sessions;
    private readonly ISetService _sets;
    private readonly IExerciseService _exercises;
    private readonly ICategoryService _categories;
    private readonly IExerciseCatalogService _catalog;
    private readonly ISettingsService _settings;

    public TodayViewModel(
        ISessionService sessions,
        ISetService sets,
        IExerciseService exercises,
        ICategoryService categories,
        IExerciseCatalogService catalog,
        ISettingsService settings)
    {
        _sessions = sessions;
        _sets = sets;
        _exercises = exercises;
        _categories = categories;
        _catalog = catalog;
        _settings = settings;

        SelectedGoal = _settings.Goal; // load persisted goal
    }

    // --- Recommendation state/helpers ---
    private int? _lastRecommendedReps;
    private double? _lastRecommendedWeight;

    // Per-session anchors by exercise (first meaningful set of this session)
    private readonly Dictionary<int, Performance> _sessionAnchors = new();

    private static double RoundToStep(double value, double stepKg)
        => Math.Round(value / stepKg, MidpointRounding.AwayFromZero) * stepKg;

    private const double RoundStepKg = 2.5; // global plate rounding

    private static double EnforceMinDelta(double newWeight, double lastWeight, double minDeltaKg)
    {
        var delta = newWeight - lastWeight;
        if (Math.Abs(delta) < minDeltaKg)
            newWeight = lastWeight + Math.Sign(delta == 0 ? 1 : delta) * minDeltaKg;
        return newWeight;
    }

    private static (double weight, int reps) FirstSetProgression(Performance lastWorkingLike, GoalConfig cfg)
    {
        // If last time was a decent working set (within goal rep range, RPE <= 8.5), nudge up a bit.
        bool wasWorking =
            lastWorkingLike.Reps >= cfg.RepsLow &&
            lastWorkingLike.Reps <= cfg.RepsHigh &&
            (!lastWorkingLike.Rpe.HasValue || lastWorkingLike.Rpe.Value <= 8.5);

        if (wasWorking)
        {
            // +2.5 kg minimum OR about +2.5% (whichever is larger), then target mid-range reps
            var bumped = Math.Max(lastWorkingLike.Weight * 1.025, lastWorkingLike.Weight + 2.5);
            return (bumped, (cfg.RepsLow + cfg.RepsHigh) / 2);
        }

        // If last was too hard, keep weight and aim mid-range
        return (lastWorkingLike.Weight, (cfg.RepsLow + cfg.RepsHigh) / 2);
    }

    // --- UI state ---
    [ObservableProperty] private WorkoutSession? currentSession;

    [ObservableProperty] private ObservableCollection<WorkoutCategory> categoryOptions = new();
    [ObservableProperty] private WorkoutCategory? selectedCategory;

    [ObservableProperty] private ObservableCollection<Exercise> exerciseOptions = new();
    [ObservableProperty] private Exercise? selectedExercise;

    [ObservableProperty] private TrainingGoal selectedGoal = TrainingGoal.Hypertrophy; // default
    [ObservableProperty] private string sessionHeader = string.Empty;

    partial void OnSelectedCategoryChanged(WorkoutCategory? value) => _ = FilterExercisesForCategoryAsync(value);

    partial void OnSelectedExerciseChanged(Exercise? value) => _ = RecommendFromLastSessionAsync(value);

    partial void OnSelectedGoalChanged(TrainingGoal value)
    {
        // persist whenever user switches goal
        _settings.Goal = value;
        // Recompute suggestion for current exercise (if any)
        if (SelectedExercise != null)
            _ = RecommendFromLastSessionAsync(SelectedExercise);
    }

    [ObservableProperty] private bool hasActiveSession;

    // Inputs
    [ObservableProperty] private string reps = string.Empty;
    [ObservableProperty] private string weightText = string.Empty;

    [ObservableProperty] private bool hasExercisesInCategory;

    private async Task FilterExercisesForCategoryAsync(WorkoutCategory? cat)
    {
        var all = await _exercises.GetAllAsync();
        var list = (cat == null)
            ? all.OrderBy(e => e.Name).ToList()
            : all.Where(e => e.CategoryId == cat.Id).OrderBy(e => e.Name).ToList();

        ExerciseOptions = new ObservableCollection<Exercise>(list);
        HasExercisesInCategory = ExerciseOptions.Count > 0;
        SelectedExercise = ExerciseOptions.FirstOrDefault();
    }

    // RPE options
    public sealed class RpeOption
    {
        public string Display { get; }
        public double? Value { get; }
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

    // Today's sets
    [ObservableProperty] private ObservableCollection<SetDisplay> todaysSets = new();

    [RelayCommand]
    public async Task Load()
    {
        await _categories.SeedDefaultsAsync();

        var cats = await _categories.GetAllAsync();
        CategoryOptions = new ObservableCollection<WorkoutCategory>(cats);
        if (SelectedCategory == null)
            SelectedCategory = CategoryOptions.FirstOrDefault();

        await FilterExercisesForCategoryAsync(SelectedCategory);

        CurrentSession = await _sessions.GetOpenSessionAsync();

        if (CurrentSession != null)
        {
            var displayNo = await _sessions.GetDisplayNumberAsync(CurrentSession.Id);
            SessionHeader = $"Session #{displayNo}";
            HasActiveSession = true;

            var nameById = ExerciseOptions.ToDictionary(e => e.Id, e => e.Name);
            var raw = await _sets.GetBySessionAsync(CurrentSession.Id);

            TodaysSets = new ObservableCollection<SetDisplay>(
                raw.Select(s => new SetDisplay
                {
                    Id = s.Id,
                    TimestampUtc = s.TimestampUtc,
                    ExerciseName = nameById.TryGetValue(s.ExerciseId, out var exName) ? exName : $"#{s.ExerciseId}",
                    SetNumber = s.SetNumber,
                    Reps = s.Reps,
                    Weight = s.Weight,
                    Rpe = s.Rpe
                }));
        }
        else
        {
            SessionHeader = string.Empty;
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

        var flow = await Shell.Current.DisplayActionSheet("Add exercise", "Cancel", null, "Pick existing", "Create new");
        if (string.IsNullOrEmpty(flow) || flow == "Cancel") return;

        if (flow == "Pick existing")
        {
            var pool = await _exercises.GetUncategorizedAsync();
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
            var chosenName = await Shell.Current.DisplayActionSheet($"Add to {SelectedCategory.Name}", "Cancel", null, names);
            if (string.IsNullOrEmpty(chosenName) || chosenName == "Cancel") return;

            var picked = pool.First(e => e.Name == chosenName);
            await _exercises.ReassignCategoryAsync(picked.Id, SelectedCategory.Id);

            await FilterExercisesForCategoryAsync(SelectedCategory);
            SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == picked.Id);
            return;
        }

        // Create new (with suggestions)
        var typed = (await Shell.Current.DisplayPromptAsync(
            "New Exercise", $"Add to: {SelectedCategory.Name}", "Add", "Cancel", "Exercise name"))?
            .Trim();

        if (string.IsNullOrWhiteSpace(typed)) return;

        var nameToUse = typed;
        var suggestions = await _catalog.SearchAsync(typed, 15);
        if (suggestions.Count > 0)
        {
            var options = suggestions.Select(s => s.Name).ToList();
            options.Insert(0, $"Use \"{typed}\"");
            var picked = await Shell.Current.DisplayActionSheet("Suggestions", "Cancel", null, options.ToArray());
            if (!string.IsNullOrEmpty(picked) && picked != "Cancel")
            {
                nameToUse = picked.StartsWith("Use \"", StringComparison.Ordinal) ? typed : picked;
            }
        }

        // Check if already exists (case-insensitive)
        var existing = await _exercises.GetByNameAsync(nameToUse);
        if (existing != null)
        {
            await _exercises.ReassignCategoryAsync(existing.Id, SelectedCategory.Id);
            await FilterExercisesForCategoryAsync(SelectedCategory);
            SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == existing.Id);
            return;
        }

        var added = await _exercises.AddAsync(nameToUse, SelectedCategory.Id);
        await FilterExercisesForCategoryAsync(SelectedCategory);
        SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == added.Id);
    }

    [RelayCommand]
    public async Task StartSession()
    {
        CurrentSession = await _sessions.StartSessionAsync();
        _sessionAnchors.Clear();
        TodaysSets.Clear();
        HasActiveSession = true;

        var displayNo = await _sessions.CountAsync();
        SessionHeader = $"Session #{displayNo}";
    }

    [RelayCommand]
    public async Task AddSet()
    {
        if (CurrentSession == null)
        {
            await Shell.Current.DisplayAlert("Start a session", "You need to start a session before adding sets.", "OK");
            return;
        }
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
        if (!double.TryParse(WeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) || weight < 0)
        {
            await Shell.Current.DisplayAlert("Invalid weight", "Enter a non-negative number.", "OK");
            return;
        }

        double? rpeToSave = SelectedRpe?.Value;

        var entry = await _sets.AddAsync(
            sessionId: CurrentSession.Id,
            exerciseId: SelectedExercise.Id,
            reps: repsValue,
            weight: weight,
            rpe: rpeToSave);

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

        // Capture first set of this exercise in this session as the anchor
        if (!_sessionAnchors.ContainsKey(SelectedExercise.Id))
        {
            _sessionAnchors[SelectedExercise.Id] = new Performance
            {
                Weight = entry.Weight,
                Reps = entry.Reps,
                Rpe = entry.Rpe
            };
        }

        Reps = string.Empty;
        SelectedRpe = null;

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
        _sessionAnchors.Clear();
        SessionHeader = string.Empty;
    }

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
        try { added = await _categories.AddAsync(name.Trim()); }
        catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); return; }

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

        try { await _categories.RenameAsync(SelectedCategory.Id, newName.Trim()); }
        catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); return; }

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
            await _exercises.UnassignAllInCategoryAsync(SelectedCategory.Id);
            await _categories.DeleteAsync(SelectedCategory.Id);
        }
        catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); return; }

        var cats = await _categories.GetAllAsync();
        CategoryOptions = new ObservableCollection<WorkoutCategory>(cats);
        SelectedCategory = CategoryOptions.FirstOrDefault();
        await FilterExercisesForCategoryAsync(SelectedCategory);
    }

    [RelayCommand]
    private async Task RenameExercise()
    {
        if (SelectedExercise == null) return;

        var newName = await Shell.Current.DisplayPromptAsync(
            "Rename Exercise", "New name", "Save", "Cancel", initialValue: SelectedExercise.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;

        try { await _exercises.RenameAsync(SelectedExercise.Id, newName.Trim()); }
        catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); return; }

        var id = SelectedExercise.Id;
        await FilterExercisesForCategoryAsync(SelectedCategory);
        SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == id);
    }

    [RelayCommand]
    private async Task MoveExercise()
    {
        if (SelectedExercise == null) return;

        var cats = CategoryOptions?.ToList() ?? new();
        if (cats.Count == 0)
        {
            await Shell.Current.DisplayAlert("No categories", "Create a category first.", "OK");
            return;
        }

        var names = cats.Select(c => c.Name).ToArray();
        var chosen = await Shell.Current.DisplayActionSheet("Move to category", "Cancel", null, names);
        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

        var target = cats.First(c => c.Name == chosen);

        try { await _exercises.ReassignCategoryAsync(SelectedExercise.Id, target.Id); }
        catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); return; }

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

    // ---------- Goal Configuration ----------
    private static GoalConfig GetConfig(TrainingGoal goal) => goal switch
    {
        TrainingGoal.Strength => new GoalConfig
        {
            RepsLow = 3,
            RepsHigh = 6,
            TargetRir = 3.0,
            MaxStepUpPct = 0.03,
            MaxStepDnPct = 0.04,
            FatigueDropPct = 0.05,
            MinDeltaKgCompound = 2.5,
            MinDeltaKgIsolation = 1.25
        },
        TrainingGoal.Hypertrophy => new GoalConfig
        {
            RepsLow = 6,
            RepsHigh = 12,
            TargetRir = 2.0,
            MaxStepUpPct = 0.04,
            MaxStepDnPct = 0.06,
            FatigueDropPct = 0.07,
            MinDeltaKgCompound = 2.5,
            MinDeltaKgIsolation = 1.25
        },
        TrainingGoal.Endurance => new GoalConfig
        {
            RepsLow = 12,
            RepsHigh = 20,
            TargetRir = 3.0,
            MaxStepUpPct = 0.05,
            MaxStepDnPct = 0.08,
            FatigueDropPct = 0.08,
            MinDeltaKgCompound = 2.5,
            MinDeltaKgIsolation = 1.0
        },
        _ => throw new ArgumentOutOfRangeException()
    };

    // ---------- Recommendation logic ----------
    private async Task RecommendFromLastSessionAsync(Exercise? exercise)
    {
        if (exercise == null) return;

        // Resolve goal config
        var cfg = GetConfig(SelectedGoal);

        IReadOnlyList<SetEntry> history;
        try { history = await _sets.GetLastSessionSetsForExerciseAsync(exercise.Id); }
        catch { return; }

        // Latest set for this exercise today (if any)
        var lastToday = TodaysSets
            .Where(s => string.Equals(s.ExerciseName, exercise.Name, StringComparison.Ordinal))
            .OrderByDescending(s => s.SetNumber)
            .FirstOrDefault();

        // --- First-set progressive overload (once per exercise per session) ---
        if (lastToday == null &&
            !_sessionAnchors.ContainsKey(exercise.Id) &&
            history != null && history.Count > 0)
        {
            var baseline = PickBaselineFromHistory(history);

            var (w, r) = FirstSetProgression(baseline, cfg);
            w = RoundToStep(w, RoundStepKg);
            r = Math.Clamp(r, cfg.RepsLow, cfg.RepsHigh);

            Reps = r.ToString();
            WeightText = w.ToString("0.##", CultureInfo.InvariantCulture);
            _lastRecommendedReps = r;
            _lastRecommendedWeight = w;

            // Cache this session's anchor for the exercise
            _sessionAnchors[exercise.Id] = new Performance { Weight = w, Reps = r, Rpe = baseline.Rpe };
            return; // done
        }

        // Choose an anchor: today → cached session anchor → baseline from history → default
        Performance anchor;
        if (lastToday != null)
        {
            anchor = new Performance { Weight = lastToday.Weight, Reps = lastToday.Reps, Rpe = lastToday.Rpe };
        }
        else if (_sessionAnchors.TryGetValue(exercise.Id, out var cached))
        {
            anchor = cached;
        }
        else if (history != null && history.Count > 0)
        {
            // Use baseline, but DO NOT cache here; first actual set or first-set block will cache.
            anchor = PickBaselineFromHistory(history);
        }
        else
        {
            // No info: use typed weight or a conservative default
            double cw = (double.TryParse(WeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w > 0) ? w : 20.0;
            int cr = Math.Max(cfg.RepsLow, 8);
            cw = RoundToStep(cw, RoundStepKg);
            Reps = cr.ToString();
            WeightText = cw.ToString("0.##", CultureInfo.InvariantCulture);
            _lastRecommendedReps = cr;
            _lastRecommendedWeight = cw;
            return;
        }

        // Estimate e1RM from anchor
        var anchorE1 = EstimateE1Rm(anchor.Weight, anchor.Reps, anchor.Rpe);

        // Aim mid-range at target RIR
        int targetReps = (cfg.RepsLow + cfg.RepsHigh) / 2;
        double inferredRir = InferRirFromReps(anchor.Reps, targetReps, anchor.Rpe);

        double rawSuggested = WeightForRepsAtRir(anchorE1, targetReps, cfg.TargetRir);

        // If last was easier than desired (inferredRir > target) → increase; harder → decrease
        var bias = Math.Clamp((inferredRir - cfg.TargetRir) * 0.02, -cfg.MaxStepDnPct, cfg.MaxStepUpPct);
        rawSuggested *= (1.0 + bias);

        if ((anchor.Rpe ?? 8.0) >= 9.0)
            rawSuggested *= (1.0 - cfg.FatigueDropPct);

        double chosenWeight = rawSuggested;

        if (lastToday != null)
        {
            // Decide min jump by compound vs isolation from catalog (best-effort)
            bool isCompound =
                (await _catalog.SearchAsync(exercise.Name, 1)).FirstOrDefault()?.IsCompound ?? true;

            double minDelta = isCompound ? cfg.MinDeltaKgCompound : cfg.MinDeltaKgIsolation;

            chosenWeight = EnforceMinDelta(chosenWeight, lastToday.Weight, minDelta);
        }

        chosenWeight = RoundToStep(chosenWeight, RoundStepKg);

        int predictedReps = PredictRepsAtWeight(anchorE1, chosenWeight, cfg.TargetRir);
        int chosenReps = Math.Clamp(predictedReps, cfg.RepsLow, cfg.RepsHigh);

        Reps = chosenReps.ToString();
        WeightText = chosenWeight.ToString("0.##", CultureInfo.InvariantCulture);
        _lastRecommendedReps = chosenReps;
        _lastRecommendedWeight = chosenWeight;
    }

    private sealed class Performance
    {
        public double Weight { get; set; }
        public int Reps { get; set; }
        public double? Rpe { get; set; }
    }

    private static Performance PickBaselineFromHistory(IReadOnlyList<SetEntry> history)
    {
        var working = history
            .Where(s => s.Rpe.HasValue && s.Rpe.Value >= 7.0 && s.Rpe.Value <= 9.0)
            .OrderByDescending(s => s.TimestampUtc)
            .ToList();

        SetEntry seed = (working.Count > 0)
            ? working.OrderByDescending(s => s.Weight).ThenByDescending(s => s.TimestampUtc).First()
            : history.OrderByDescending(s => s.Weight).ThenByDescending(s => s.Reps).First();

        return new Performance { Weight = seed.Weight, Reps = seed.Reps, Rpe = seed.Rpe };
    }

    // --- e1RM helpers ---
    private static double EstimateE1Rm(double weight, int reps, double? rpe)
    {
        reps = Math.Max(1, reps);
        double e1 = weight * (1.0 + reps / 30.0);

        if (rpe.HasValue)
        {
            var rir = Math.Clamp(10.0 - rpe.Value, 0.0, 5.0);
            var pct = Pct1RmFromRepsAndRir(reps, rir);
            if (pct > 0.20 && pct < 1.20)
                e1 = weight / pct;
        }
        return e1;
    }

    private static double InferRirFromReps(int achievedReps, int targetReps, double? rpe)
    {
        if (rpe.HasValue) return Math.Clamp(10.0 - rpe.Value, 0.0, 5.0);
        int diff = targetReps - achievedReps;    // positive if fewer than target (harder)
        double inferred = 2.0 + (-diff);         // center around RIR 2
        return Math.Clamp(inferred, 0.0, 5.0);
    }

    private static double WeightForRepsAtRir(double e1rm, int reps, double rir)
    {
        double pct = Pct1RmFromRepsAndRir(reps, rir);
        return Math.Max(0.0, e1rm * pct);
    }

    private static int PredictRepsAtWeight(double e1rm, double weight, double rir)
    {
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

    private static double Pct1RmFromRepsAndRir(int reps, double rir)
    {
        reps = Math.Clamp(reps, 1, 20);
        rir = Math.Clamp(rir, 0.0, 5.0);

        // Approximate %1RM at RIR 0
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

        double pct = pctAtRir0 * (1.0 - 0.025 * rir); // ~2.5% per RIR
        return Math.Clamp(pct, 0.30, 1.10);
    }

    // Local config container
    private sealed class GoalConfig
    {
        public int RepsLow { get; init; }
        public int RepsHigh { get; init; }
        public double TargetRir { get; init; }
        public double MaxStepUpPct { get; init; }
        public double MaxStepDnPct { get; init; }
        public double FatigueDropPct { get; init; }
        public double MinDeltaKgCompound { get; init; }
        public double MinDeltaKgIsolation { get; init; }
    }
}
