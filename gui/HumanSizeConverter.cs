using NeoSmart.PrettySize;
using System.Globalization;
using System.Windows.Data;

namespace FishSyncClient.Gui;

public class HumanSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long longSize)
        {
            var prettySize = new PrettySize(longSize);
            return prettySize.Format(UnitBase.Base10);
        }
        else
        {
            return value;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}