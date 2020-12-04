using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VKTalker.Models;
using VKTalker.ViewModels;

namespace VKTalker.Converter
{
    public class ImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

                var key = value as string;
                if (key is null) return null;
                var filePath = GlobalImageDictionary.Get(key);
                var b = new Bitmap(filePath);
                    return b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}