using System.Collections.ObjectModel;
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

        var name = await Shell.Current.DisplayPromptAsync("New Exercise", $"Add to: {SelectedCategory.Name}", "Add", "Cancel", "Exercise name");
        if (string.IsNullOrWhiteSpace(name)) return;

        var trimmed = name.Trim();

        // 1) Reuse if it already exists (case-insensitive)
        var existing = await _exercises.GetByNameAsync(trimmed);
        Exercise target;

        if (existing != null)
        {
            await _exercises.ReassignCategoryAsync(existing.Id, SelectedCategory.Id);
            target = existing;
        }
        else
        {
            target = await _exercises.AddAsync(trimmed, SelectedCategory.Id);
        }
       
        // Reload list and select the newly added exercise
        await FilterExercisesForCategoryAsync(SelectedCategory);
        SelectedExercise = ExerciseOptions.FirstOrDefault(e => e.Id == target.Id);
    }

    [RelayCommand]
    public async Task StartSession()
    {
        CurrentSession = await _sessions.StartSessionAsync();
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
    private async Task RecommendFromLastSessionAsync(Exercise? exercise)
    {
        if (exercise == null) return;

        IReadOnlyList<SetEntry> lastSessionSets;
        try
        {
            lastSessionSets = await _sets.GetLastSessionSetsForExerciseAsync(exercise.Id);
        }
        catch
        {
            return; // DB/service issue → keep placeholders
        }

        if (lastSessionSets == null || lastSessionSets.Count == 0)
            return;

        // Baseline from LAST SESSION
        var working = lastSessionSets
            .Where(s => s.Rpe.HasValue && s.Rpe.Value >= 7 && s.Rpe.Value <= 9)
            .ToList();

        var basis = working.Count > 0 ? working : lastSessionSets;

        // weight = mode (most frequent) of basis; reps = median for that weight
        double chosenWeight;
        int chosenReps;

        var groupedByWeight = basis
            .GroupBy(s => Math.Round(s.Weight, 3))
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .ToList();

        if (groupedByWeight.Count > 0)
        {
            var topGroup = groupedByWeight[0];
            chosenWeight = topGroup.Key;
            var repsList = topGroup.Select(s => s.Reps).OrderBy(r => r).ToList();
            chosenReps = repsList[repsList.Count / 2];
        }
        else
        {
            chosenWeight = basis.Max(s => s.Weight);
            var repsList = basis.Select(s => s.Reps).OrderBy(r => r).ToList();
            chosenReps = repsList[repsList.Count / 2];
        }

        // Average RPE across basis → gentle nudge
        var avgRpe = basis.Where(s => s.Rpe.HasValue).Select(s => s.Rpe!.Value).DefaultIfEmpty(8.0).Average();
        if (avgRpe <= 6.0)
        {
            chosenWeight *= 1.02;   // +2%
            chosenReps = Math.Max(1, chosenReps + 1);
        }
        else if (avgRpe >= 8.5)
        {
            chosenWeight *= 0.98;   // -2%
            if (chosenReps > 1) chosenReps -= 1;
        }

        // Reactive adjustment from TODAY — reuse a single lastToday variable
        var lastToday = TodaysSets
            .Where(s => string.Equals(s.ExerciseName, exercise.Name, StringComparison.Ordinal))
            .OrderByDescending(s => s.SetNumber)
            .FirstOrDefault();

        if (lastToday != null)
        {
            var lastTodayRpe = lastToday.Rpe ?? 8.0;

            // If last set was MAX effort or clearly tough → do NOT increase; gently decrease
            if (lastTodayRpe >= 9.0)
            {
                chosenWeight *= 0.95; // -5%
                chosenReps = Math.Max(1, Math.Min(chosenReps, lastToday.Reps - 1));
            }
            // If last set was very easy → small increase
            else if (lastTodayRpe <= 4.0)
            {
                chosenWeight *= 1.03; // +3%
                chosenReps = Math.Max(chosenReps, lastToday.Reps + 1);
            }
            else if (lastTodayRpe >= 7.0 && lastTodayRpe <= 8.5)
            {
                // Stay near what you just did if it was a good working effort
                chosenReps = Math.Max(chosenReps, lastToday.Reps);
                chosenWeight = Math.Max(chosenWeight, lastToday.Weight);
            }

            // Consider underperforming relative to our last recommendation (if we had one)
            if (_lastRecommendedReps.HasValue && lastToday.Reps < (int)Math.Ceiling(_lastRecommendedReps.Value * 0.75))
            {
                // Significant underperformance → reduce weight and avoid rep increases
                chosenWeight *= 0.95;
                chosenReps = Math.Min(chosenReps, lastToday.Reps);
            }
        }

        // If we have a set for this exercise today, enforce a minimum ±5 kg change when adjusting
        if (lastToday != null && !chosenWeight.Equals(lastToday.Weight))
        {
            chosenWeight = EnforceMinDelta(chosenWeight, lastToday.Weight, minDeltaKg: 5.0);
        }

        // Round to practical plate steps (2.5 kg increments)
        chosenWeight = RoundToStep(chosenWeight, stepKg: 2.5);

        // Prefill UI and store this recommendation for the next evaluation
        Reps = chosenReps.ToString();
        WeightText = chosenWeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _lastRecommendedReps = chosenReps;
        _lastRecommendedWeight = chosenWeight;
    }
}
