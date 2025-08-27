using WorkoutTracker.Services;

namespace WorkoutTracker;

public partial class App : Application
{
    // Allow global access to DI container
    public static new App Current => (App)Application.Current;
    public IServiceProvider Services { get; }

    public App(IServiceProvider services, Database db)
    {
        InitializeComponent();

        // Store the service provider for ServiceHelper
        Services = services;

        // Initialize database
        _ = db.InitAsync();

        MainPage = new AppShell();
    }
}
