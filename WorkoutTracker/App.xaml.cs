using WorkoutTracker.Services;

namespace WorkoutTracker;

public partial class App : Application
{
    public IServiceProvider Services { get; }

    public App(IServiceProvider services, Database db, IExerciseCatalogService exerciseCatalog)
    {
        InitializeComponent();
        Services = services;

#if ANDROID
        // Avoid deadlocks at startup on Android
        _ = InitializeDbAsync(db, exerciseCatalog);
#else
        // Desktop: block so first pages have guaranteed tables
        db.InitAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        exerciseCatalog.EnsureCreatedAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        exerciseCatalog.SeedDefaultsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#endif
        try
        {
            var catalog = services.GetRequiredService<IExerciseCatalogService>();

#if ANDROID
            _ = catalog.SeedDefaultsAsync(); // non-blocking for Android
#else
            catalog.SeedDefaultsAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Catalog seed failed: " + ex);
        }

        MainPage = new AppShell();
    }

    private static async Task InitializeDbAsync(Database db, IExerciseCatalogService exerciseCatalog)
    {
        try
        {
            await db.InitAsync().ConfigureAwait(false);
            await exerciseCatalog.EnsureCreatedAsync().ConfigureAwait(false);
            await exerciseCatalog.SeedDefaultsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Startup init failed: " + ex);
        }
    }

    public static new App Current => (App)Application.Current!;
}
