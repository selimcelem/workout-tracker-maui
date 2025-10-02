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
    [ObservableProperty] private ObservableCollection<Exercise> exerciseOptions = new();
    [ObservableProperty] private Exercise? selectedExercise;
    partial void OnSelectedExerciseChanged(Exercise? value)
    {
        _ = RecommendFromLastSessionAsync(value);
    }
    [ObservableProperty] private bool hasActiveSession;

    // Inputs
    // Reps/Weight as strings so placeholders show and Entry.Text never throws
    [ObservableProperty] private string reps = string.Empty;
    [ObservableProperty] private string weightText = string.Empty;

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

    public TodayViewModel(ISessionService sessions, ISetService sets, IExerciseService exercises)
    {
        _sessions = sessions;
        _sets = sets;
        _exercises = exercises;
    }

    // Load current session + exercises + existing sets
    [RelayCommand]
    public async Task Load()
    {
        CurrentSession = await _sessions.GetOpenSessionAsync();

        var options = await _exercises.GetAllAsync();
        ExerciseOptions = new ObservableCollection<Exercise>(options);

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
