using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VKTalker.ViewModels;

namespace VKTalker.Converter
{
    public class ImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

                var name = value as string;

               
                if (name != null && File.Exists(Path.Combine(MainWindowViewModel.PhotoFolder, name)))
                {
                    var b = new Bitmap(Path.Combine(MainWindowViewModel.PhotoFolder, name));
                    return b;
                }
                    
                else
                    return null;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}