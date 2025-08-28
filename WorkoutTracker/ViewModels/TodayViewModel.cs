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

    // --- UI state ---
    [ObservableProperty] private WorkoutSession? currentSession;
    [ObservableProperty] private ObservableCollection<Exercise> exerciseOptions = new();
    [ObservableProperty] private Exercise? selectedExercise;
    [ObservableProperty] private bool hasActiveSession;

    // Reps stays a string so Entry.Text binding never throws
    [ObservableProperty] private string reps = string.Empty;

    [ObservableProperty] private double weight;
    [ObservableProperty] private double? rpe;

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
                    Id = s.Id, // NEW
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

        if (Weight < 0)
        {
            await Shell.Current.DisplayAlert("Invalid weight", "Weight cannot be negative.", "OK");
            return;
        }

        // 3) Save (SetService assigns correct next SetNumber per session+exercise)
        var entry = await _sets.AddAsync(
            sessionId: CurrentSession.Id,
            exerciseId: SelectedExercise.Id,
            reps: repsValue,
            weight: Weight,
            rpe: Rpe
        );

        // 4) Update UI list (include Id)
        TodaysSets.Add(new SetDisplay
        {
            Id = entry.Id, // NEW
            TimestampUtc = entry.TimestampUtc,
            ExerciseName = SelectedExercise.Name,
            SetNumber = entry.SetNumber,
            Reps = entry.Reps,
            Weight = entry.Weight,
            Rpe = entry.Rpe
        });

        // 5) Small UX reset (keep weight for convenience)
        Reps = string.Empty;
        Rpe = null;
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

    // NEW: swipe-to-delete support
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
}
