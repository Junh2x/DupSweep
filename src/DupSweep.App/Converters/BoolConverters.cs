using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using DupSweep.App.Services;

namespace DupSweep.App.Converters;

/// <summary>
/// XAML 바인딩용 값 변환기 모음
/// Bool, Int, 바이트배열 등을 Visibility, ImageSource 등으로 변환
/// </summary>

/// <summary>
/// bool을 Visibility로 변환 (true → Visible)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// bool을 Visibility로 변환 (false → Visible)
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

/// <summary>
/// bool 값 반전
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

/// <summary>
/// int를 Visibility로 변환 (0보다 크면 Visible)
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseIntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToPausePlayIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isPaused && isPaused ? "Play" : "Pause";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToPauseResumeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isPaused && isPaused
            ? LanguageService.Instance.GetString("Converter.Resume")
            : LanguageService.Instance.GetString("Converter.Pause");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 바이트 배열을 BitmapImage로 변환 (썸네일 표시용)
/// </summary>
public class ByteArrayToImageSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not byte[] data || data.Length == 0)
        {
            return null!;
        }

        var image = new BitmapImage();
        using var stream = new MemoryStream(data);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 파일 크기(바이트)를 읽기 쉬운 문자열로 변환 (KB, MB, GB 등)
/// </summary>
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return "0 B";
        }

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 전체 경로에서 폴더명만 추출
/// </summary>
public class PathToFolderNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            return Path.GetFileName(path) ?? path;
        }
        return value ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            return new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD)); // Light blue background
        }
        return new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA)); // Default background
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToDriveColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDrive && isDrive)
        {
            return new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)); // Primary color for drives
        }
        return new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)); // Accent color for folders
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isHidden && isHidden)
        {
            return 0.5;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 사용량 비율을 색상으로 변환 (정상/경고/위험)
/// </summary>
public class UsageToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double usage)
        {
            if (usage >= 90)
                return new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)); // Red - critical
            if (usage >= 75)
                return new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)); // Orange - warning
            return new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)); // Green - normal
        }
        return new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)); // Default blue
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent && parameter is string maxWidthStr && double.TryParse(maxWidthStr, out double maxWidth))
        {
            return Math.Max(2, percent / 100.0 * maxWidth);
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

/// <summary>
/// 퍼센트를 Canvas.Left 위치로 변환 (바 차트용)
/// </summary>
public class PercentToCanvasLeftConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double percent && values[1] is double containerWidth)
        {
            return percent / 100.0 * containerWidth;
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 퍼센트를 너비로 변환 (바 차트용)
/// </summary>
public class PercentToWidthMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double percent && values[1] is double containerWidth)
        {
            return Math.Max(1, percent / 100.0 * containerWidth);
        }
        return 1.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LessThanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double val && parameter is string paramStr && double.TryParse(paramStr, out double threshold))
        {
            return val < threshold;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 파일 경로에서 Windows Shell 썸네일을 가져오는 변환기
/// Windows 탐색기가 캐시한 썸네일을 직접 사용하여 빠름
/// </summary>
public class FilePathToThumbnailConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> _cache = new();
    private const int ThumbnailSize = 96;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string filePath || string.IsNullOrEmpty(filePath))
            return null;

        // 캐시 확인
        if (_cache.TryGetValue(filePath, out var cached))
            return cached;

        // Shell 썸네일 가져오기
        var thumbnail = GetShellThumbnail(filePath, ThumbnailSize);
        _cache.TryAdd(filePath, thumbnail);
        return thumbnail;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static BitmapSource? GetShellThumbnail(string filePath, int size)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var shellItem);
            if (hr != 0 || shellItem == null)
                return null;

            var imageFactory = (IShellItemImageFactory)shellItem;
            var requestedSize = new SIZE { cx = size, cy = size };

            // SIIGBF_THUMBNAILONLY (0x01) | SIIGBF_BIGGERSIZEOK (0x08)
            hr = imageFactory.GetImage(requestedSize, 0x01 | 0x08, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
                return null;

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
        }
    }

    #region Native Methods

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage([In] SIZE size, [In] int flags, out IntPtr phbm);
    }

    #endregion
}
