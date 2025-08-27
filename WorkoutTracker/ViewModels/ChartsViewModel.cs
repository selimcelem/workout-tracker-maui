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
    private readonly ISessionService _sessions;
    private readonly ISetService _sets;

    // OLE Automation date valid range (same as .NET)
    private const double OA_MIN = -657435.0;  // 0100-01-01
    private const double OA_MAX = 2958466.0; // 9999-12-31

    // Safe labeler that ignores invalid values
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

    // Create axes in the ctor so we can adjust Min/Max dynamically later
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

            // Build volume per session for the selected exercise
            var sessions = await _sessions.GetRecentAsync(90) ?? new List<WorkoutSession>();
            var points = new List<DateTimePoint>();

            foreach (var s in sessions.OrderBy(s => s.DateUtc))
            {
                var sets = await _sets.GetBySessionAsync(s.Id) ?? new List<SetEntry>();
                var vol = sets.Where(x => x.ExerciseId == SelectedExercise.Id)
                              .Sum(x => x.Reps * x.Weight);

                if (vol > 0)
                {
                    var day = s.DateUtc.ToLocalTime().Date;
                    points.Add(new DateTimePoint(day, vol));
                }
            }

            VolumeSeries.Clear();
            VolumeSeries.Add(new LineSeries<DateTimePoint>
            {
                Values = points,
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(),
                Fill = null
            });

            // Clamp the X axis to the data range to avoid invalid labeler inputs
            if (points.Count > 0)
            {
                // DateTimePoint.DateTime is the date; guard with OA range check
                var minOa = points.Min(p => p.DateTime.ToOADate());
                var maxOa = points.Max(p => p.DateTime.ToOADate());

                // tiny padding (1 day) while staying within OA bounds
                var pad = TimeSpan.FromDays(1).TotalDays;
                var min = Math.Max(OA_MIN, minOa - pad);
                var max = Math.Min(OA_MAX, maxOa + pad);

                XAxes[0].MinLimit = min;
                XAxes[0].MaxLimit = max;
            }
            else
            {
                // No data: let the chart decide (and labeler will ignore invalids)
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
