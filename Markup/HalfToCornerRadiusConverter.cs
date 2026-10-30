using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Panak.Markup {
    [ValueConversion(typeof(double), typeof(CornerRadius))]
    public class HalfToCornerRadiusConverter : IValueConverter {
        public static readonly HalfToCornerRadiusConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is double d && d > 0 && !double.IsNaN(d) && !double.IsInfinity(d)) {
                return new CornerRadius(d / 2.0);
            }
            return new CornerRadius(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
