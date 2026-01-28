using DupSweep.Core.Models;

namespace DupSweep.Tests.Models;

public class ScanConfigTests
{
    [Fact]
    public void ScanConfig_DefaultValues_AreCorrect()
    {
        // Act
        var config = new ScanConfig();

        // Assert
        Assert.Empty(config.Directories);
        Assert.True(config.UseHashComparison);
        Assert.True(config.UseSizeComparison);
        Assert.False(config.UseResolutionComparison);
        Assert.True(config.UseImageSimilarity);
        Assert.True(config.UseVideoSimilarity);
        Assert.False(config.MatchCreatedDate);
        Assert.False(config.MatchModifiedDate);
        Assert.True(config.ScanAllFiles); // 기본값: 모든 파일 스캔
        Assert.True(config.ScanImages);
        Assert.True(config.ScanVideos);
        Assert.Equal(85, config.ImageSimilarityThreshold);
        Assert.Equal(85, config.VideoSimilarityThreshold);
        Assert.Equal(128, config.ThumbnailSize);
        Assert.Equal(Environment.ProcessorCount, config.ParallelThreads);
        Assert.Equal(0, config.MinFileSize);
        Assert.Equal(long.MaxValue, config.MaxFileSize);
        Assert.False(config.FollowSymlinks);
        Assert.False(config.IncludeHiddenFiles);
        Assert.True(config.RecursiveScan);
    }

    [Fact]
    public void GetSupportedExtensions_BothEnabled_ReturnsAllExtensions()
    {
        // Arrange
        var config = new ScanConfig
        {
            ScanAllFiles = false, // 확장자 필터링 활성화
            ScanImages = true,
            ScanVideos = true
        };

        // Act
        var extensions = config.GetSupportedExtensions().ToList();

        // Assert
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".jpeg", extensions);
        Assert.Contains(".png", extensions);
        Assert.Contains(".gif", extensions);
        Assert.Contains(".mp4", extensions);
        Assert.Contains(".avi", extensions);
        Assert.Contains(".mkv", extensions);
    }

    [Fact]
    public void GetSupportedExtensions_OnlyImages_ReturnsImageExtensions()
    {
        // Arrange
        var config = new ScanConfig
        {
            ScanAllFiles = false, // 확장자 필터링 활성화
            ScanImages = true,
            ScanVideos = false
        };

        // Act
        var extensions = config.GetSupportedExtensions().ToList();

        // Assert
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".png", extensions);
        Assert.DoesNotContain(".mp4", extensions);
        Assert.DoesNotContain(".avi", extensions);
    }

    [Fact]
    public void GetSupportedExtensions_OnlyVideos_ReturnsVideoExtensions()
    {
        // Arrange
        var config = new ScanConfig
        {
            ScanAllFiles = false, // 확장자 필터링 활성화
            ScanImages = false,
            ScanVideos = true
        };

        // Act
        var extensions = config.GetSupportedExtensions().ToList();

        // Assert
        Assert.DoesNotContain(".jpg", extensions);
        Assert.DoesNotContain(".png", extensions);
        Assert.Contains(".mp4", extensions);
        Assert.Contains(".avi", extensions);
    }

    [Fact]
    public void GetSupportedExtensions_NoneEnabled_ReturnsEmpty()
    {
        // Arrange
        var config = new ScanConfig
        {
            ScanAllFiles = false, // 확장자 필터링 활성화
            ScanImages = false,
            ScanVideos = false
        };

        // Act
        var extensions = config.GetSupportedExtensions().ToList();

        // Assert
        Assert.Empty(extensions);
    }

    [Fact]
    public void GetSupportedExtensions_IncludesAllImageFormats()
    {
        // Arrange
        var config = new ScanConfig { ScanAllFiles = false, ScanImages = true, ScanVideos = false };
        var expectedFormats = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico", ".heic", ".heif" };

        // Act
        var extensions = config.GetSupportedExtensions().ToList();

        // Assert
        foreach (var format in expectedFormats)
        {
            Assert.Contains(format, extensions);
        }
    }

    [Fact]
    public void GetSupportedExtensions_IncludesAllVideoFormats()
    {
        // Arrange
        var config = new ScanConfig { ScanAllFiles = false, ScanImages = false, ScanVideos = true };
        var expectedFormats = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp" };

        // Act
        var extensions = config.GetSupportedExtensions().ToList();

        // Assert
        foreach (var format in expectedFormats)
        {
            Assert.Contains(format, extensions);
        }
    }

    [Fact]
    public void Directories_CanAddMultipleDirectories()
    {
        // Arrange
        var config = new ScanConfig();

        // Act
        config.Directories.Add(@"C:\Folder1");
        config.Directories.Add(@"C:\Folder2");
        config.Directories.Add(@"C:\Folder3");

        // Assert
        Assert.Equal(3, config.Directories.Count);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 50)]
    [InlineData(100, 0)]
    public void SimilarityThreshold_ValidRange(double imageThreshold, double videoThreshold)
    {
        // Arrange & Act
        var config = new ScanConfig
        {
            ImageSimilarityThreshold = imageThreshold,
            VideoSimilarityThreshold = videoThreshold
        };

        // Assert
        Assert.Equal(imageThreshold, config.ImageSimilarityThreshold);
        Assert.Equal(videoThreshold, config.VideoSimilarityThreshold);
    }
}
