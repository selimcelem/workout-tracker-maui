using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using SQLite;
using System.IO;
using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;
using WorkoutTracker.Views;
using WorkoutTracker.Models; // for Exercise, WorkoutSession, SetEntry

namespace WorkoutTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
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

        // ViewModels (Transient: fresh instance per page)
        builder.Services.AddTransient<ExercisesViewModel>();
        builder.Services.AddTransient<TodayViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();

        // Pages (Transient)
        builder.Services.AddTransient<ExercisesPage>();
        builder.Services.AddTransient<TodayPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<SessionDetailPage>();

        var app = builder.Build();

        // Ensure DB schema is created before any page loads
        var connection = app.Services.GetRequiredService<SQLiteAsyncConnection>();
        connection.CreateTableAsync<Exercise>().GetAwaiter().GetResult();
        connection.CreateTableAsync<WorkoutSession>().GetAwaiter().GetResult();
        connection.CreateTableAsync<SetEntry>().GetAwaiter().GetResult();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("UNHANDLED: " + e.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("TASK EX: " + e.Exception);
        };

        return app;
    }
}
