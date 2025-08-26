using System.Globalization;

namespace WorkoutTracker.Converters;

public class NullToBoolConverter : IValueConverter
{
    // If parameter == "Invert" => returns true when value == null
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = (parameter as string)?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
        bool isNull = value is null;
        return invert ? isNull : !isNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
