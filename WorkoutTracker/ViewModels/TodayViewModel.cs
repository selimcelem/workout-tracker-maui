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
    [ObservableProperty] private int reps;
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
                    TimestampUtc = s.TimestampUtc,
                    ExerciseName = nameById.TryGetValue(s.ExerciseId, out var n) ? n : $"#{s.ExerciseId}",
                    SetNumber = s.SetNumber,
                    Reps = s.Reps,
                    Weight = s.Weight,
                    Rpe = s.Rpe
                }));
        }
        else
        {
            TodaysSets.Clear();
        }
    }

    [RelayCommand]
    public async Task StartSession()
    {
        // Start a new session (or return existing one for today)
        CurrentSession = await _sessions.StartSessionAsync();
        TodaysSets.Clear();
        HasActiveSession = true;
    }

    [RelayCommand]
    public async Task AddSet()
    {
        if (CurrentSession is null || SelectedExercise is null) return;

        // Next set number for this exercise in the current session
        var existingSets = await _sets.GetBySessionAsync(CurrentSession.Id);
        var next = existingSets
            .Where(s => s.ExerciseId == SelectedExercise.Id)
            .Select(s => s.SetNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

        // Save to DB
        var entry = await _sets.AddAsync(
        sessionId: CurrentSession.Id,
        exerciseId: SelectedExercise.Id,
        reps: Reps,
        weight: Weight,
        rpe: Rpe
        );


        // Append to UI list with the exercise *name*
        TodaysSets.Add(new SetDisplay
        {
            TimestampUtc = entry.TimestampUtc,
            ExerciseName = SelectedExercise.Name,
            SetNumber = entry.SetNumber,
            Reps = entry.Reps,
            Weight = entry.Weight,
            Rpe = entry.Rpe
        });

        // Small UX reset (keep weight for convenience)
        Reps = 0;
        Rpe = null;
    }

    [RelayCommand]
    public async Task EndSession()
    {
        if (CurrentSession == null)
            return;

        await _sessions.EndSessionAsync(CurrentSession.Id);

        CurrentSession = null;
        TodaysSets.Clear();
        HasActiveSession = false;
    }
}

public class SetDisplay
{
    public DateTime TimestampUtc { get; set; }
    public string ExerciseName { get; set; } = "";
    public int SetNumber { get; set; }
    public int Reps { get; set; }
    public double Weight { get; set; }
    public double? Rpe { get; set; }
}
