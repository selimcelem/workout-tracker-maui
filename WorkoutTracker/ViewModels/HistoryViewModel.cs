using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly ISetService _setService;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? status; // for quick diagnostics in UI (optional)

    public ObservableCollection<SessionListItem> RecentSessions { get; } = new();

    public HistoryViewModel(ISessionService sessionService, ISetService setService)
    {
        _sessionService = sessionService;
        _setService = setService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Status = "Loading...";

            RecentSessions.Clear();

            var recent = await _sessionService.GetRecentAsync(30);
            foreach (var s in recent)
            {
                var sets = await _setService.GetBySessionAsync(s.Id);
                RecentSessions.Add(new SessionListItem
                {
                    Id = s.Id,
                    Title = s.DateUtc.ToLocalTime().ToString("dddd, dd MMM yyyy HH:mm"),
                    Subtitle = sets.Count == 1 ? "1 set" : $"{sets.Count} sets",
                    Notes = s.Notes
                });
            }

            Status = RecentSessions.Count == 0 ? "No sessions yet." : null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();
}

public class SessionListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? Notes { get; set; }
}
