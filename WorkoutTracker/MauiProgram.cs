using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using SQLite;
using System.IO;

using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;
using WorkoutTracker.Views;

// ✨ Required by LiveCharts + Skia on MAUI
using LiveChartsCore.SkiaSharpView.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace WorkoutTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // 👇👇 These two lines register the handlers LiveCharts needs.
            .UseLiveCharts()
            .UseSkiaSharp() // <-- This fixes “Handler not found for SKCanvasView”
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // SQLite connection
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "workouts.db");
        var conn = new SQLiteAsyncConnection(dbPath);

        // Core singletons
        builder.Services.AddSingleton(conn);
        builder.Services.AddSingleton<Database>();

        // Services (interfaces -> implementations)
        builder.Services.AddSingleton<IExerciseService, ExerciseService>();
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<ISetService, SetService>();

        // ViewModels
        builder.Services.AddTransient<ExercisesViewModel>();
        builder.Services.AddTransient<TodayViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<ChartsViewModel>();

        // Pages
        builder.Services.AddTransient<ExercisesPage>();
        builder.Services.AddTransient<TodayPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<SessionDetailPage>();
        builder.Services.AddTransient<ChartsPage>();

        return builder.Build();
    }
}
