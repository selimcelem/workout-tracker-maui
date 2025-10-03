using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using SQLite;
using System.IO;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;
using WorkoutTracker.Views;

// Required by LiveCharts + Skia on MAUI
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
            // These two lines are required for the chart handlers on Android
            .UseLiveCharts()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // SQLite connections
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "workouts.db");

        // 1) Use a synchronous connection just for schema/migrations (no async blocking on UI thread)
        using (var sync = new SQLiteConnection(dbPath))
        {
            sync.CreateTable<Exercise>();
            sync.CreateTable<WorkoutSession>();
            sync.CreateTable<SetEntry>();

            // One-time migration: add CategoryId to Exercise if it doesn't exist
            try
            {
                sync.Execute("ALTER TABLE Exercise ADD COLUMN CategoryId INTEGER;");
            }
            catch
            {
                // Column already exists → ignore
            }

            sync.CreateTable<WorkoutCategory>();
        }

        // 2) Use the normal async connection for the rest of the app
        var conn = new SQLiteAsyncConnection(dbPath);

        // Core singletons
        builder.Services.AddSingleton(conn);
        builder.Services.AddSingleton<Database>();

        // Services (interfaces -> implementations)
        builder.Services.AddSingleton<IExerciseService, ExerciseService>();
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<ISetService, SetService>();
        builder.Services.AddSingleton<ICategoryService, CategoryService>();
        
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

        var app = builder.Build();

        return app;
    }
}
