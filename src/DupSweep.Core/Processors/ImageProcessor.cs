using DupSweep.Core.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

/// <summary>
/// 이미지 처리기
/// ImageSharp 라이브러리를 사용하여 이미지 분석 및 처리
/// </summary>
public class ImageProcessor : IImageProcessor
{
    /// <summary>
    /// dHash(Difference Hash) 알고리즘으로 이미지 지각 해시 계산
    /// aHash보다 구조적 차이 감지에 우수하며, 색상만 비슷한 이미지의 오탐을 줄임
    /// </summary>
    public async Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgba32>(filePath, cancellationToken);
            // dHash: 9x8로 리사이즈 (가로 9픽셀 = 8개의 차분 비교)
            image.Mutate(x => x.Resize(9, 8).Grayscale());

            ulong hash = 0;
            int bit = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    // 인접 픽셀 비교: 왼쪽 픽셀이 오른쪽보다 밝으면 1
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

    /// <summary>
    /// 색상 차분 해시(Color dHash) 계산
    /// R-G 색차의 인접 픽셀 비교로 색상 분포를 64비트 해시로 인코딩
    /// dHash(구조)와 결합하여 색상이 다른 이미지의 오탐을 방지
    /// </summary>
    public async Task<ulong?> ComputeColorHashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgba32>(filePath, cancellationToken);
            // 그레이스케일 변환 없이 리사이즈만 (색상 정보 보존)
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
                        // R-G 색차의 인접 비교 (chrominance difference hash)
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

    /// <summary>
    /// 이미지 썸네일 생성
    /// </summary>
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

    /// <summary>
    /// 이미지 해상도(너비, 높이) 추출
    /// </summary>
    public async Task<(int Width, int Height)> GetImageResolutionAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var info = await Image.IdentifyAsync(filePath, cancellationToken);
            return (info.Width, info.Height);
        }
        catch
        {
            return (0, 0);
        }
    }
}
