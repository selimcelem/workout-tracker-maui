using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using SQLite;
using System.IO;
using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;
using WorkoutTracker.Views;

namespace WorkoutTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Add platform guard to suppress CA1416
        #if WINDOWS
        var builder = MauiApp.CreateBuilder();
        #else
        var builder = MauiApp.CreateBuilder();
        #endif

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "workouts.db");
        var conn = new SQLiteAsyncConnection(dbPath);

        builder.Services.AddSingleton(conn);
        builder.Services.AddSingleton<Database>();
        builder.Services.AddSingleton<IExerciseService, ExerciseService>();

        builder.Services.AddSingleton<ExercisesViewModel>();
        builder.Services.AddTransient<ExercisesPage>();
        builder.Services.AddTransient<TodayPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddSingleton<HistoryViewModel>();

        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<ISetService, SetService>();

        builder.Services.AddSingleton<TodayViewModel>();

        return builder.Build();
    }
}
