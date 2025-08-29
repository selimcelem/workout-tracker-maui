using WorkoutTracker.Services;

namespace WorkoutTracker;

public partial class App : Application
{
    public IServiceProvider Services { get; }

    // Ensure DB is ready before any page runs
    public App(IServiceProvider services, Database db)
    {
        InitializeComponent();

        Services = services;

        // BLOCK until the DB is initialized, so first pages can query safely.
        // This avoids "empty history until relaunch" issues due to async init.
        try
        {
            db.InitAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // If something goes wrong, show a minimal error page.
            MainPage = new ContentPage
            {
                Content = new VerticalStackLayout
                {
                    Padding = 24,
                    Children =
                    {
                        new Label { Text = "Database initialization failed.", FontAttributes = FontAttributes.Bold },
                        new Label { Text = ex.ToString(), FontSize = 12 }
                    }
                }
            };
            return;
        }

        MainPage = new AppShell();
    }

    public static new App Current => (App)Application.Current!;
}
