using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class TodayViewModel : ObservableObject
{
    private readonly ISessionService _sessions;
    private readonly ISetService _sets;
    private readonly IExerciseService _exercises;

    [ObservableProperty] private WorkoutSession? currentSession;
    [ObservableProperty] private ObservableCollection<Exercise> exerciseOptions = new();
    [ObservableProperty] private Exercise? selectedExercise;

    [ObservableProperty] private int reps;
    [ObservableProperty] private double weight;
    [ObservableProperty] private double? rpe;

    [ObservableProperty] private ObservableCollection<SetEntry> todaysSets = new();

    public TodayViewModel(ISessionService sessions, ISetService sets, IExerciseService exercises)
    {
        _sessions = sessions;
        _sets = sets;
        _exercises = exercises;
    }

    [RelayCommand]
    public async Task Load()
    {
        CurrentSession = await _sessions.GetOpenSessionAsync();
        var options = await _exercises.GetAllAsync();
        ExerciseOptions = new ObservableCollection<Exercise>(options);

        if (CurrentSession != null)
        {
            var list = await _sets.GetBySessionAsync(CurrentSession.Id);
            TodaysSets = new ObservableCollection<SetEntry>(list);
        }
    }

    [RelayCommand]
    public async Task StartSession()
    {
        CurrentSession = await _sessions.StartSessionAsync();
        TodaysSets.Clear();
    }

    [RelayCommand]
    public async Task AddSet()
    {
        if (CurrentSession is null || SelectedExercise is null) return;
        var next = await _sets.GetNextSetNumberAsync(CurrentSession.Id, SelectedExercise.Id);
        var entry = await _sets.AddAsync(CurrentSession.Id, SelectedExercise.Id, next, Reps, Weight, Rpe);

        TodaysSets.Add(entry);
        // small UX reset
        Reps = 0; Rpe = null; // keep weight as convenience
    }
}
