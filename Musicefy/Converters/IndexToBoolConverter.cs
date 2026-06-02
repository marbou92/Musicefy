using System;
using System.Globalization;
using System.Windows.Data;

namespace Musicefy.Converters
{
    /// <summary>
    /// Converts between an enum/int value and a boolean for RadioButton bindings.
    /// Supports both integer comparison and string comparison.
    /// When the bound value is an enum, it compares the enum's integer value
    /// to the ConverterParameter. When it's a string, it does string comparison.
    /// </summary>
    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            // If value is an enum, compare its integer value to the parameter
            if (value is Enum enumValue)
            {
                if (int.TryParse(parameter.ToString(), out int intParameter))
                {
                    return Convert.ToInt32(enumValue) == intParameter;
                }
            }

            // Fallback: string comparison (for old int-based bindings)
            string stringValue = value.ToString();
            string stringParameter = parameter.ToString();

            return stringValue.Equals(stringParameter, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int intParameter))
                {
                    // If the target type is an enum, convert the int to that enum
                    if (targetType.IsEnum)
                        return Enum.ToObject(targetType, intParameter);
                    return intParameter;
                }
            }
            return Binding.DoNothing;
        }
    }
}
