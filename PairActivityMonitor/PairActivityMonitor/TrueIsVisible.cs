using System;
using System.Globalization;
using System.Windows.Data;

namespace PairActivityMonitor
{
	public class TrueIsVisible : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((bool)value) return System.Windows.Visibility.Visible;
			else return System.Windows.Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
