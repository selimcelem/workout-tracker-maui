// File: WorkoutTracker/ViewModels/ChartsViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
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

            // Build chart points
            var points = groups
                .Where(g => g.Volume > 0)
                .Select(g => new ObservablePoint(g.Day.ToOADate(), g.Volume))
                .ToList();

            // Visible line/marker (fallback to scatter if only 1 point)
            var color = SKColors.DeepSkyBlue;

            if (points.Count == 1)
            {
                var scatter = new ScatterSeries<ObservablePoint>
                {
                    Values = points,
                    GeometrySize = 16,
                    Fill = new SolidColorPaint(color),   // ScatterSeries uses Fill
                    Stroke = new SolidColorPaint(color) { StrokeThickness = 2 }
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
                    Stroke = new SolidColorPaint(color) { StrokeThickness = 3 },
                    Fill = null
                };
                VolumeSeries.Add(series);
            }

            // Clamp axes
            if (points.Count > 0)
            {
                var minOa = points.Min(p => p.X);
                var maxOa = points.Max(p => p.X);
                var padX = TimeSpan.FromDays(1).TotalDays;

                // Avoid Math.Min/Max overload weirdness
                var minLimitX = minOa - padX; if (minLimitX < OA_MIN) minLimitX = OA_MIN;
                var maxLimitX = maxOa + padX; if (maxLimitX > OA_MAX) maxLimitX = OA_MAX;
                XAxes[0].MinLimit = (double?)minLimitX;
                XAxes[0].MaxLimit = (double?)maxLimitX;

                var yMin = points.Min(p => p.Y);
                var yMax = points.Max(p => p.Y);
                if (yMin == yMax) { yMin -= 1; yMax += 1; }
                var padY = (yMax - yMin) * 0.10;
                YAxes[0].MinLimit = yMin - padY;
                YAxes[0].MaxLimit = yMax + padY;

                StatusText = $"{points.Count} point(s) · {SelectedExercise.Name}";
            }
            else
            {
                XAxes[0].MinLimit = null;
                XAxes[0].MaxLimit = null;
                YAxes[0].MinLimit = null;
                YAxes[0].MaxLimit = null;

                // Helpful message if no data
                StatusText = $"No data for {SelectedExercise.Name} in last 90 days on this device. " +
                             $"Add sets in Today, then return.";
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
