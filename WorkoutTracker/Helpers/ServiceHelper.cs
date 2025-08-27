namespace WorkoutTracker.Helpers;

public static class ServiceHelper
{
    public static T GetService<T>() where T : notnull
    {
        var provider = App.Current.Services
            ?? throw new InvalidOperationException("Service provider not available.");

        if (provider.GetService(typeof(T)) is T service)
            return service;

        throw new InvalidOperationException($"Service not registered: {typeof(T)}");
    }
}
