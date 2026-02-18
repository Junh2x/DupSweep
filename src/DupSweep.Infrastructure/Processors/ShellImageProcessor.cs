using System.Runtime.InteropServices;
using DupSweep.Core.Models;
using DupSweep.Core.Processors;
using SixLabors.ImageSharp.Processing;

namespace DupSweep.Infrastructure.Processors;

/// <summary>
/// Windows Shell API를 사용하여 파일 탐색기 썸네일을 가져오는 이미지 처리기
/// </summary>
public class ShellImageProcessor : IImageProcessor
{
    private readonly FallbackImageProcessor _fallbackProcessor = new();


    public async Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        // 지각 해시는 원본 ImageProcessor 사용
        return await _fallbackProcessor.ComputePerceptualHashAsync(filePath, config, cancellationToken);
    }

    public async Task<ulong?> ComputeColorHashAsync(string filePath, CancellationToken cancellationToken)
    {
        return await _fallbackProcessor.ComputeColorHashAsync(filePath, cancellationToken);
    }

    public Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var size = Math.Max(32, config.ThumbnailSize);
            return GetShellThumbnail(filePath, size);
        }, cancellationToken);
    }

    public async Task<(int Width, int Height)> GetImageResolutionAsync(string filePath, CancellationToken cancellationToken)
    {
        return await _fallbackProcessor.GetImageResolutionAsync(filePath, cancellationToken);
    }

    private static byte[]? GetShellThumbnail(string filePath, int size)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var shellItem);
            if (hr != 0 || shellItem == null)
                return null;

            var imageFactory = (IShellItemImageFactory)shellItem;
            var requestedSize = new SIZE { cx = size, cy = size };

            // SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK
            hr = imageFactory.GetImage(requestedSize, 0x01 | 0x08, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
                return null;

            return ConvertHBitmapToJpegBytes(hBitmap);
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

    private static byte[]? ConvertHBitmapToJpegBytes(IntPtr hBitmap)
    {
        try
        {
            using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    #region Native Methods and Interfaces

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

/// <summary>
/// 지각 해시 및 해상도 추출용 간단한 폴백 프로세서
/// </summary>
internal class FallbackImageProcessor : IImageProcessor
{
    public async Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(filePath, cancellationToken);
            image.Mutate(x => x.Resize(9, 8).Grayscale());

            ulong hash = 0;
            int bit = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < 8; x++)
                    {
                        if (row[x].R > row[x + 1].R)
                        {
                            hash |= 1UL << bit;
                        }
                        bit++;
                    }
                }
            });

            return hash;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ulong?> ComputeColorHashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(filePath, cancellationToken);
            image.Mutate(x => x.Resize(9, 8));

            ulong hash = 0;
            int bit = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < 8; x++)
                    {
                        int chromaDiff1 = row[x].R - row[x].G;
                        int chromaDiff2 = row[x + 1].R - row[x + 1].G;
                        if (chromaDiff1 > chromaDiff2)
                        {
                            hash |= 1UL << bit;
                        }
                        bit++;
                    }
                }
            });

            return hash;
        }
        catch
        {
            return null;
        }
    }

    public Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        // Shell 썸네일 실패 시 사용되지 않음
        return Task.FromResult<byte[]?>(null);
    }

    public async Task<(int Width, int Height)> GetImageResolutionAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var info = await SixLabors.ImageSharp.Image.IdentifyAsync(filePath, cancellationToken);
            return (info.Width, info.Height);
        }
        catch
        {
            return (0, 0);
        }
    }
}
