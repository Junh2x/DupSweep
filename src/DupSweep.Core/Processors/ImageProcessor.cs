using DupSweep.Core.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

public class ImageProcessor : IImageProcessor
{
    public async Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgba32>(filePath, cancellationToken);
            image.Mutate(x => x.Resize(8, 8).Grayscale());

            double total = 0;
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        total += row[x].R;
                    }
                }
            });

            var average = total / 64d;
            ulong hash = 0;
            int bit = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        if (row[x].R >= average)
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

    public async Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgba32>(filePath, cancellationToken);
            var size = Math.Max(32, config.ThumbnailSize);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(size, size)
            }));

            await using var stream = new MemoryStream();
            await image.SaveAsJpegAsync(stream, cancellationToken);
            return stream.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
