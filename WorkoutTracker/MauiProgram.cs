using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using SQLite;
using System.IO;
using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;
using WorkoutTracker.Views;
using WorkoutTracker.Models; // Exercise, WorkoutSession, SetEntry

// NEW for chart hosting (rc5+ requires both)
using SkiaSharp.Views.Maui.Controls.Hosting;     // .UseSkiaSharp()
using LiveChartsCore.SkiaSharpView.Maui;          // .UseLiveCharts()

namespace WorkoutTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Order matters on rc5+: chain these AFTER UseMauiApp
            .UseSkiaSharp()
            .UseLiveCharts()
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

        // Services
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

        var app = builder.Build();

        // Ensure DB schema exists
        var connection = app.Services.GetRequiredService<SQLiteAsyncConnection>();
        connection.CreateTableAsync<Exercise>().GetAwaiter().GetResult();
        connection.CreateTableAsync<WorkoutSession>().GetAwaiter().GetResult();
        connection.CreateTableAsync<SetEntry>().GetAwaiter().GetResult();

        return app;
    }
}
