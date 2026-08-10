using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;

namespace AvalNeroSercher.Converters
{
    public class BitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is System.Drawing.Bitmap systemBitmap)
            {
                using var memory = new MemoryStream();
                systemBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                return new Avalonia.Media.Imaging.Bitmap(memory);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}