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
        _ = InitializeDbAsync(db);
#else
    db.InitAsync().ConfigureAwait(false).GetAwaiter().GetResult();
#endif

        // Ensure catalog exists and is seeded
        var catalog = Services.GetRequiredService<IExerciseCatalogService>();
        _ = Task.Run(async () =>
        {
            try
            {
                await catalog.EnsureCreatedAsync();
                await catalog.SeedDefaultsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Catalog init failed: " + ex);
            }
        });

        MainPage = new AppShell();
    }

    private static async Task InitializeDbAsync(Database db)
    {
        try
        {
            await db.InitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DB init failed: " + ex);
        }
    }

    public static new App Current => (App)Application.Current!;
}
