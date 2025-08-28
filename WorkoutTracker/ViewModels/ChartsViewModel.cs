using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class ChartsViewModel : ObservableObject
{
    private readonly IExerciseService _exercises;
    private readonly ISessionService _sessions; // kept if you need later
    private readonly ISetService _sets;

    // OLE Automation date valid range guards
    private const double OA_MIN = -657435.0;
    private const double OA_MAX = 2958466.0;

    private static string FormatOaDate(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return string.Empty;
        if (v < OA_MIN || v > OA_MAX) return string.Empty;
        return DateTime.FromOADate(v).ToString("MMM d");
    }

    public ObservableCollection<Exercise> Exercises { get; } = new();

    [ObservableProperty] private Exercise? selectedExercise;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<ISeries> VolumeSeries { get; } = new();

    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; } =
    {
        new Axis { Labeler = v => $"{v:0}" }
    };

    public ChartsViewModel(IExerciseService exercises, ISessionService sessions, ISetService sets)
    {
        _exercises = exercises;
        _sessions = sessions;
        _sets = sets;

        XAxes = new[]
        {
            new Axis
            {
                Labeler = FormatOaDate,
                UnitWidth = TimeSpan.FromDays(1).TotalDays,
                MinStep   = TimeSpan.FromDays(1).TotalDays
            }
        };
    }

    public async Task InitializeAsync()
    {
        if (Exercises.Count > 0) return;

        var all = await _exercises.GetAllAsync() ?? new List<Exercise>();
        foreach (var ex in all.OrderBy(e => e.Name))
            Exercises.Add(ex);

        SelectedExercise = Exercises.FirstOrDefault();
        if (SelectedExercise != null)
            await RefreshChartAsync();
    }

    partial void OnSelectedExerciseChanged(Exercise? value)
    {
        _ = RefreshChartAsync();
    }

    [RelayCommand]
    public Task Refresh() => RefreshChartAsync();

    private async Task RefreshChartAsync()
    {
        if (SelectedExercise == null) return;
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            // Pull sets for the exercise over the last 90 days
            var sinceUtc = DateTime.UtcNow.Date.AddDays(-90);
            var entries = await _sets.GetByExerciseSinceAsync(SelectedExercise.Id, sinceUtc)
                          ?? new List<SetEntry>();

            // Group by local day and sum volume (reps * weight)
            var groups = entries
                .GroupBy(e => e.TimestampUtc.ToLocalTime().Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Volume = g.Sum(x => x.Reps * x.Weight)
                })
                .Where(x => x.Volume > 0)
                .OrderBy(x => x.Day)
                .ToList();

            VolumeSeries.Clear();

            var points = groups.Select(g => new DateTimePoint(g.Day, g.Volume)).ToList();

            VolumeSeries.Add(new LineSeries<DateTimePoint>
            {
                Values = points,
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(),
                Fill = null
            });

            // Clamp axis to data range to keep labeler safe
            if (points.Count > 0)
            {
                var minOa = points.Min(p => p.DateTime.ToOADate());
                var maxOa = points.Max(p => p.DateTime.ToOADate());
                var pad = TimeSpan.FromDays(1).TotalDays;

                XAxes[0].MinLimit = Math.Max(OA_MIN, minOa - pad);
                XAxes[0].MaxLimit = Math.Min(OA_MAX, maxOa + pad);
            }
            else
            {
                XAxes[0].MinLimit = null;
                XAxes[0].MaxLimit = null;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
