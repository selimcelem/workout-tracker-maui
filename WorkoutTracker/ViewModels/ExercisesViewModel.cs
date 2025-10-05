using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

public partial class ExercisesViewModel : ObservableObject
{
    private readonly IExerciseService _exercises;
    private readonly ICategoryService _categories;

    [ObservableProperty] private ObservableCollection<Exercise> items = new();
    [ObservableProperty] private string newExerciseName = "";

    public ExercisesViewModel(IExerciseService exercises, ICategoryService categories)
    {
        _exercises = exercises;
        _categories = categories;
    }

    [RelayCommand]
    public async Task Load()
    {
        var list = await _exercises.GetAllAsync();
        Items = new ObservableCollection<Exercise>(list);
    }

    [RelayCommand]
    public async Task Add()
    {
        var name = (NewExerciseName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        await _exercises.AddAsync(name);
        NewExerciseName = "";
        await Load();
    }

    [RelayCommand]
    public async Task Delete(int id)
    {
        await _exercises.DeleteAsync(id);
        await Load();
    }

    [RelayCommand]
    private async Task AssignToCategory(Exercise? exercise)
    {
        if (exercise == null) return;

        var cats = await _categories.GetAllAsync();
        if (cats.Count == 0)
        {
            await Shell.Current.DisplayAlert("No categories", "Create a category first.", "OK");
            return;
        }

        var choice = await Shell.Current.DisplayActionSheet(
            "Assign to category", "Cancel", null, cats.Select(c => c.Name).ToArray());

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        var target = cats.First(c => c.Name == choice);
        await _exercises.ReassignCategoryAsync(exercise.Id, target.Id);

        // Refresh the list so the change is visible
        await Load();
    }
}
