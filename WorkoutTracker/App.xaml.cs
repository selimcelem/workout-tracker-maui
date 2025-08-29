using WorkoutTracker.Services;

namespace WorkoutTracker;

public partial class App : Application
{
    public IServiceProvider Services { get; }

    public App(IServiceProvider services, Database db)
    {
        InitializeComponent();
        Services = services;

#if ANDROID
        // Avoid deadlocks at startup on Android
        _ = InitializeDbAsync(db);
#else
        // Desktop: block so first pages have guaranteed tables
        db.InitAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#endif

        MainPage = new AppShell();
    }

    private static async Task InitializeDbAsync(Database db)
    {
        try { await db.InitAsync().ConfigureAwait(false); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DB init failed: " + ex);
        }
    }

    public static new App Current => (App)Application.Current!;
}
