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

            var summary = $"Set {entry.SetNumber}: {entry.Reps} reps × {entry.Weight:0.##} kg";
            if (entry.Rpe.HasValue) summary += $" · RPE: {entry.Rpe.Value:0.#}";

            Sets.Add(new SetRow
            {
                Id = entry.Id,
                Time = entry.TimestampUtc.ToLocalTime().ToString("HH:mm"),
                ExerciseName = exercise?.Name ?? $"Exercise #{entry.ExerciseId}",
                Summary = summary
            });
        }
    }
    private async void OnDeleteSetInvoked(object sender, EventArgs e)
    {
        if (sender is not SwipeItem swipeItem) return;
        if (swipeItem.BindingContext is not SetRow row) return;

        var confirm = await DisplayAlert("Delete set",
            $"Delete {row.ExerciseName}?\n{row.Summary}", "Delete", "Cancel");
        if (!confirm) return;

        // delete from DB
        await _sets.DeleteAsync(row.Id);

        // delete from UI
        var match = Sets.FirstOrDefault(s => s.Id == row.Id);
        if (match != null) Sets.Remove(match);
    }
}

public class SetRow
{
    public int Id { get; set; }
    public string Time { get; set; } = string.Empty;
    public string ExerciseName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

