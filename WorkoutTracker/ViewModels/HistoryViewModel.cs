using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly ISetService _setService;

    // Bindable flag to enable/disable the “Clear All” button
    [ObservableProperty] private bool hasAnySessions;

    // Explicit properties (avoid source-generator issues)
    public ObservableCollection<SessionListItem> RecentSessions { get; } = new();
    public SessionListItem? SelectedSession { get; set; }

    public HistoryViewModel(ISessionService sessionService, ISetService setService)
    {
        _sessionService = sessionService;
        _setService = setService;
    }

    // Load recent sessions with their set counts
    [RelayCommand]
    public async Task LoadAsync(int take = 30)
    {
        RecentSessions.Clear();

        var sessions = await _sessionService.GetRecentAsync(take) ?? new List<WorkoutSession>();

        foreach (var s in sessions.OrderByDescending(s => s.DateUtc))
        {
            var sets = await _setService.GetBySessionAsync(s.Id) ?? new List<SetEntry>();
            var setCount = sets.Count;

            var title = s.DateUtc.ToLocalTime().ToString("dddd, dd MMM yyyy HH:mm");
            var subtitle = setCount == 1 ? "1 set" : $"{setCount} sets";
            if (!string.IsNullOrWhiteSpace(s.Notes))
                subtitle += " • notes";

            RecentSessions.Add(new SessionListItem
            {
                SessionId = s.Id,
                DateUtc = s.DateUtc,
                Title = title,
                Subtitle = subtitle
            });
        }

        HasAnySessions = RecentSessions.Count > 0;
    }

    // Open a session details page
    [RelayCommand]
    private async Task OpenSession(SessionListItem? item)
    {
        if (item == null) return;
        SelectedSession = null; // clear selection so reselect works
        await Shell.Current.GoToAsync($"SessionDetailPage?sessionId={item.SessionId}");
    }

    // Delete entire session (all sets + session)
    [RelayCommand]
    private async Task DeleteSession(SessionListItem? item)
    {
        if (item == null) return;

        var ok = await Shell.Current.DisplayAlert(
            "Delete session",
            $"Delete session on {item.DateUtc.ToLocalTime():yyyy-MM-dd} and all its sets?",
            "Delete", "Cancel");
        if (!ok) return;

        await _setService.DeleteBySessionAsync(item.SessionId);
        await _sessionService.DeleteAsync(item.SessionId);

        RecentSessions.Remove(item);
        HasAnySessions = RecentSessions.Count > 0;
    }

    // Remove all sessions (and their sets) using existing APIs
    [RelayCommand]
    public async Task ClearAllAsync()
    {
        var all = await _sessionService.GetRecentAsync(int.MaxValue) ?? new List<WorkoutSession>();
        foreach (var s in all)
        {
            await _setService.DeleteBySessionAsync(s.Id);
            await _sessionService.DeleteAsync(s.Id);
        }

        RecentSessions.Clear();
        HasAnySessions = false;
    }


    public class SessionListItem
    {
        public int SessionId { get; set; }
        public DateTime DateUtc { get; set; }
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
    }
}
