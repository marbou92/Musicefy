using System;
using System.Globalization;
using System.Windows.Data;

namespace Musicefy.Converters
{
    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

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
                    return intParameter;
                }
            }
            return Binding.DoNothing;
        }
    }
}
