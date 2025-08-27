using WorkoutTracker.Views;

namespace WorkoutTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(SessionDetailPage), typeof(SessionDetailPage));
        }
    }
}
