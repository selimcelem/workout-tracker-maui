using WorkoutTracker.Services;

namespace WorkoutTracker;

public partial class App : Application
{
    public App(Database db)
    {
        InitializeComponent();
        _ = db.InitAsync();
        MainPage = new AppShell();
    }
}
