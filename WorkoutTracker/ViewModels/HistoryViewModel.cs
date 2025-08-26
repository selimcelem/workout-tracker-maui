using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ISessionService _sessions;
    private readonly ISetService _sets;
    private readonly IExerciseService _exercises;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private ObservableCollection<SessionItem> recentSessions = new();
    [ObservableProperty] private SessionItem? selectedSession;

    // Note: use a distinct item type to avoid clashing with TodayViewModel.SetDisplay
    [ObservableProperty] private ObservableCollection<HistorySetRow> selectedSessionSets = new();

    public HistoryViewModel(ISessionService sessions, ISetService sets, IExerciseService exercises)
    {
        _sessions = sessions;
        _sets = sets;
        _exercises = exercises;
    }

    [RelayCommand]
    public async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            RecentSessions.Clear();

            var sessions = await _sessions.GetRecentAsync(30);
            var names = (await _exercises.GetAllAsync()).ToDictionary(e => e.Id, e => e.Name);

            foreach (var s in sessions.OrderByDescending(s => s.DateUtc))
            {
                var setCount = (await _sets.GetBySessionAsync(s.Id)).Count;
                RecentSessions.Add(new SessionItem
                {
                    Id = s.Id,
                    DateUtc = s.DateUtc,
                    Notes = s.Notes,
                    SetCount = setCount
                });
            }

            SelectedSession = RecentSessions.FirstOrDefault();
            if (SelectedSession != null)
                await LoadSetsForSelected(names);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSessionChanged(SessionItem? value)
    {
        _ = LoadSetsForSelected();
    }

    private async Task LoadSetsForSelected(Dictionary<int, string>? names = null)
    {
        if (SelectedSession == null)
        {
            SelectedSessionSets = new();
            return;
        }

        names ??= (await _exercises.GetAllAsync()).ToDictionary(e => e.Id, e => e.Name);
        var sets = await _sets.GetBySessionAsync(SelectedSession.Id);

        var list = sets
            .OrderBy(s => s.TimestampUtc)
            .Select(s => new HistorySetRow
            {
                Time = s.TimestampUtc.ToLocalTime(),
                ExerciseName = names.TryGetValue(s.ExerciseId, out var n) ? n : $"#{s.ExerciseId}",
                SetNumber = s.SetNumber,
                Reps = s.Reps,
                Weight = s.Weight,
                Rpe = s.Rpe
            })
            .ToList();

        SelectedSessionSets = new ObservableCollection<HistorySetRow>(list);
    }
}

public class SessionItem
{
    public int Id { get; set; }
    public DateTime DateUtc { get; set; }
    public string? Notes { get; set; }
    public int SetCount { get; set; }

    public string Title => $"{DateUtc.ToLocalTime():ddd, dd MMM}";
    public string Subtitle => $"{SetCount} set{(SetCount == 1 ? "" : "s")}" +
                              (string.IsNullOrWhiteSpace(Notes) ? "" : $" • {Notes}");
}

// Distinct name to avoid collision with TodayViewModel's SetDisplay
public class HistorySetRow
{
    public DateTime Time { get; set; }
    public string ExerciseName { get; set; } = "";
    public int SetNumber { get; set; }
    public int Reps { get; set; }
    public double Weight { get; set; }
    public double? Rpe { get; set; }

    public string Summary => $"Set {SetNumber}: {Reps} reps @ {Weight:0.##}" +
                             (Rpe is null ? "" : $" (RPE {Rpe:0.#})");
}
