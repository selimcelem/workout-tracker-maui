using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class ExercisesViewModel : ObservableObject
{
    private readonly IExerciseService _service;

    [ObservableProperty] private ObservableCollection<Exercise> items = new();
    [ObservableProperty] private string newExerciseName = "";

    public ExercisesViewModel(IExerciseService service) => _service = service;

    [RelayCommand]
    public async Task Load()
    {
        var list = await _service.GetAllAsync();
        Items = new ObservableCollection<Exercise>(list);
    }

    [RelayCommand]
    public async Task Add()
    {
        var name = (NewExerciseName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        await _service.AddAsync(name);
        NewExerciseName = "";
        await Load();
    }

    [RelayCommand]
    public async Task Delete(int id)
    {
        await _service.DeleteAsync(id);
        await Load();
    }
}
