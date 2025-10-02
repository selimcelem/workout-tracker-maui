// File: WorkoutTracker/ViewModels/ChartsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class ChartsViewModel : ObservableObject
{
    private readonly IExerciseService _exercises;
    private readonly ISessionService _sessions; // reserved for future
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
    [ObservableProperty] private string? statusText;

    public ObservableCollection<ISeries> VolumeSeries { get; } = new();

    // NEW: quick diagnostics to show what we computed
    public ObservableCollection<DebugPoint> DebugPoints { get; } = new();

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
        if (Exercises.Count == 0)
        {
            var all = await _exercises.GetAllAsync() ?? new List<Exercise>();
            foreach (var ex in all.OrderBy(e => e.Name))
                Exercises.Add(ex);
        }

        // Ensure we have a selection
        if (SelectedExercise == null)
            SelectedExercise = Exercises.FirstOrDefault();

        // Render
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
        if (SelectedExercise == null)
        {
            StatusText = "No exercise selected.";
            VolumeSeries.Clear();
            DebugPoints.Clear();
            return;
        }
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusText = $"Loading {SelectedExercise.Name}…";
            DebugPoints.Clear();
            VolumeSeries.Clear();

            // Get sets for selected exercise in last 90 days
            var sinceUtc = DateTime.UtcNow.Date.AddDays(-90);
            var entries = await _sets.GetByExerciseSinceAsync(SelectedExercise.Id, sinceUtc)
                          ?? new List<SetEntry>();

            // Group by local date and compute volume (reps * weight)
            var groups = entries
                .GroupBy(e => e.TimestampUtc.ToLocalTime().Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Volume = g.Sum(x => x.Reps * x.Weight)
                })
                .OrderBy(x => x.Day)
                .ToList();

            // Diagnostics list for the UI
            foreach (var g in groups)
                DebugPoints.Add(new DebugPoint { Day = g.Day, Volume = g.Volume });

            // ---- build points (X = OADate, Y = volume) ----
            var points = groups
                .Select(g => new ObservablePoint(g.Day.ToOADate(), g.Volume))
                .ToList();

            VolumeSeries.Clear();

            // bright, thick, and no animations so it shows instantly
            var color = SKColors.DeepSkyBlue;

            if (points.Count == 1)
            {
                var scatter = new ScatterSeries<ObservablePoint>
                {
                    Values = points,
                    GeometrySize = 18,
                    Fill = new SolidColorPaint(color),                // marker fill
                    Stroke = new SolidColorPaint(color) { StrokeThickness = 3 }, // marker border
                    AnimationsSpeed = TimeSpan.Zero
                };
                VolumeSeries.Add(scatter);
            }
            else
            {
                var series = new LineSeries<ObservablePoint>
                {
                    Values = points,
                    GeometrySize = 10,
                    GeometryFill = new SolidColorPaint(color),
                    GeometryStroke = new SolidColorPaint(color) { StrokeThickness = 2 },
                    Stroke = new SolidColorPaint(color) { StrokeThickness = 4, IsAntialias = true },
                    Fill = null,
                    LineSmoothness = 0,                 // straight segments
                    AnimationsSpeed = TimeSpan.Zero
                };
                VolumeSeries.Add(series);
            }

            // ---- clamp both axes to the data range ----
            if (points.Count > 0)
            {
                // X axis (dates in OADate units)
                double minX = points.Min(p => p.X ?? 0);
                double maxX = points.Max(p => p.X ?? 0);
                double padX = TimeSpan.FromDays(1).TotalDays;

                double minLimitX = Math.Max(OA_MIN, minX - padX);
                double maxLimitX = Math.Min(OA_MAX, maxX + padX);

                XAxes[0].MinLimit = minLimitX;
                XAxes[0].MaxLimit = maxLimitX;

                // Y axis (volume)
                double yMin = points.Min(p => p.Y ?? 0);
                double yMax = points.Max(p => p.Y ?? 0);
                if (yMin == yMax) { yMin -= 1; yMax += 1; } // avoid flat range
                double padY = (yMax - yMin) * 0.10;

                double minLimitY = yMin - padY;
                double maxLimitY = yMax + padY;

                YAxes[0].MinLimit = minLimitY;
                YAxes[0].MaxLimit = maxLimitY;

                StatusText = $"{points.Count} point(s) · {SelectedExercise.Name}";
            }
            else
            {
                XAxes[0].MinLimit = null;
                XAxes[0].MaxLimit = null;
                YAxes[0].MinLimit = null;
                YAxes[0].MaxLimit = null;
                StatusText = $"No data for {SelectedExercise.Name} in last 90 days.";
            }

        }
        finally
        {
            IsBusy = false;
        }
    }
}

// Small DTO for the diagnostics list
public class DebugPoint
{
    public DateTime Day { get; set; }
    public double Volume { get; set; }
}
