using System.Collections.ObjectModel;
using WorkoutTracker.Services;
using WorkoutTracker.Models;
using WorkoutTracker.Helpers;

namespace WorkoutTracker.Views;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class SessionDetailPage : ContentPage
{
    private readonly ISetService _sets;
    private readonly IExerciseService _exercises;

    public int SessionId { get; set; }

    public ObservableCollection<SetRow> Sets { get; } = new();

    // Shell/HotReload-friendly parameterless ctor
    public SessionDetailPage()
        : this(ServiceHelper.GetService<ISetService>(),
               ServiceHelper.GetService<IExerciseService>())
    { }

    // DI ctor
    public SessionDetailPage(ISetService sets, IExerciseService exercises)
    {
        InitializeComponent();
        _sets = sets;
        _exercises = exercises;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (SessionId > 0)
            await LoadSets();
    }

    private async Task LoadSets()
    {
        Sets.Clear();

        var entries = await _sets.GetBySessionAsync(SessionId) ?? new List<SetEntry>();
        var allExercises = await _exercises.GetAllAsync() ?? new List<Exercise>();

        foreach (var entry in entries.OrderBy(e => e.TimestampUtc).ThenBy(e => e.SetNumber))
        {
            var exercise = allExercises.FirstOrDefault(e => e.Id == entry.ExerciseId);

            Sets.Add(new SetRow
            {
                Time = entry.TimestampUtc.ToLocalTime().ToString("HH:mm"),
                ExerciseName = exercise?.Name ?? $"Exercise #{entry.ExerciseId}",
                Summary = $"Set {entry.SetNumber}: {entry.Reps} reps × {entry.Weight:0.##} kg"
            });
        }
    }
}

public class SetRow
{
    public string Time { get; set; } = "";
    public string ExerciseName { get; set; } = "";
    public string Summary { get; set; } = "";
}
