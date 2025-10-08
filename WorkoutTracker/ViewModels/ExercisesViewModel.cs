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
    private readonly IExerciseCatalogService _catalog;

    [ObservableProperty] private ObservableCollection<Exercise> items = new();
    [ObservableProperty] private string newExerciseName = "";

    public ExercisesViewModel(IExerciseService exercises, ICategoryService categories, IExerciseCatalogService catalog)
    {
        _exercises = exercises;
        _categories = categories;
        _catalog = catalog;
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
        var name = await PromptExerciseNameWithSuggestionsAsync();
        if (name is null) return;

        // Check if exercise already exists
        var existing = await _exercises.GetByNameAsync(name);
        if (existing != null)
        {
            await Shell.Current.DisplayAlert("Already exists",
                $"{name} is already in your list.", "OK");
            return;
        }

        // Otherwise add it normally
        await _exercises.AddAsync(name);
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

    private async Task<string?> PromptExerciseNameWithSuggestionsAsync()
    {
        var typed = (await Shell.Current.DisplayPromptAsync(
            "New Exercise", "Name", "Add", "Cancel", "e.g. Back Squat"))?.Trim();

        if (string.IsNullOrWhiteSpace(typed))
            return null;

        var nameToUse = typed;

        // Show suggestions for short fragments
        if (typed.Length <= 3)
        {
            var suggestions = await _catalog.SearchAsync(typed, 20);
            if (suggestions.Count > 0)
            {
                var options = suggestions.Select(s => s.Name)
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .ToList();
                options.Insert(0, $"Use \"{typed}\"");

                var picked = await Shell.Current.DisplayActionSheet("Suggestions", "Cancel", null, options.ToArray());
                if (string.IsNullOrEmpty(picked) || picked == "Cancel")
                    return null;

                nameToUse = picked.StartsWith("Use \"", StringComparison.Ordinal) ? typed : picked;
            }
        }

        return nameToUse;
    }

}
