using DupSweep.Core.Algorithms;
using DupSweep.Core.Models;

namespace DupSweep.Tests.Algorithms;

public class DuplicateDetectorTests
{
    private readonly DuplicateDetector _detector = new();
    private readonly ScanConfig _defaultConfig = new();

    [Fact]
    public void FindExactMatches_SameHash_ReturnsGroup()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "file1.txt", FullHash = "abc123" },
            new() { FilePath = "file2.txt", FullHash = "abc123" },
            new() { FilePath = "file3.txt", FullHash = "def456" }
        };

        // Act
        var groups = _detector.FindExactMatches(files, _defaultConfig);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Files.Count);
        Assert.Equal(DuplicateType.ExactMatch, groups[0].Type);
        Assert.Equal(100, groups[0].Similarity);
    }

    [Fact]
    public void FindExactMatches_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "file1.txt", FullHash = "abc123" },
            new() { FilePath = "file2.txt", FullHash = "def456" },
            new() { FilePath = "file3.txt", FullHash = "ghi789" }
        };

        // Act
        var groups = _detector.FindExactMatches(files, _defaultConfig);

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void FindExactMatches_MultipleGroups_ReturnsAllGroups()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "a1.txt", FullHash = "hash_a" },
            new() { FilePath = "a2.txt", FullHash = "hash_a" },
            new() { FilePath = "b1.txt", FullHash = "hash_b" },
            new() { FilePath = "b2.txt", FullHash = "hash_b" },
            new() { FilePath = "unique.txt", FullHash = "hash_unique" }
        };

        // Act
        var groups = _detector.FindExactMatches(files, _defaultConfig);

        // Assert
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void FindExactMatches_NullHash_Excluded()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "file1.txt", FullHash = "abc123" },
            new() { FilePath = "file2.txt", FullHash = null },
            new() { FilePath = "file3.txt", FullHash = "abc123" }
        };

        // Act
        var groups = _detector.FindExactMatches(files, _defaultConfig);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Files.Count);
    }

    [Fact]
    public void FindExactMatches_EmptyHash_Excluded()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "file1.txt", FullHash = "abc123" },
            new() { FilePath = "file2.txt", FullHash = "" },
            new() { FilePath = "file3.txt", FullHash = "   " },
            new() { FilePath = "file4.txt", FullHash = "abc123" }
        };

        // Act
        var groups = _detector.FindExactMatches(files, _defaultConfig);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Files.Count);
    }

    [Fact]
    public void FindExactMatches_WithMatchCreatedDate_FiltersCorrectly()
    {
        // Arrange
        var date1 = new DateTime(2023, 1, 1);
        var date2 = new DateTime(2023, 1, 2);

        var files = new List<FileEntry>
        {
            new() { FilePath = "file1.txt", FullHash = "abc", CreatedDate = date1 },
            new() { FilePath = "file2.txt", FullHash = "abc", CreatedDate = date1 },
            new() { FilePath = "file3.txt", FullHash = "abc", CreatedDate = date2 }
        };

        var config = new ScanConfig { MatchCreatedDate = true };

        // Act
        var groups = _detector.FindExactMatches(files, config);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Files.Count);
    }

    [Fact]
    public void FindSimilarImages_IdenticalHash_ReturnsGroup()
    {
        // Arrange
        ulong hash = 0x123456789ABCDEF0UL;
        var files = new List<FileEntry>
        {
            new() { FilePath = "img1.jpg", FileType = FileType.Image, PerceptualHash = hash },
            new() { FilePath = "img2.jpg", FileType = FileType.Image, PerceptualHash = hash }
        };

        // Act
        var groups = _detector.FindSimilarImages(files, 85);

        // Assert
        Assert.Single(groups);
        Assert.Equal(DuplicateType.SimilarImage, groups[0].Type);
        Assert.Equal(100, groups[0].Similarity);
    }

    [Fact]
    public void FindSimilarImages_SimilarHash_ReturnsGroup()
    {
        // Arrange - 1비트 차이
        ulong hash1 = 0x123456789ABCDEF0UL;
        ulong hash2 = 0x123456789ABCDEF1UL; // 1비트 다름 = 98.4% 유사도

        var files = new List<FileEntry>
        {
            new() { FilePath = "img1.jpg", FileType = FileType.Image, PerceptualHash = hash1 },
            new() { FilePath = "img2.jpg", FileType = FileType.Image, PerceptualHash = hash2 }
        };

        // Act
        var groups = _detector.FindSimilarImages(files, 85);

        // Assert
        Assert.Single(groups);
        Assert.True(groups[0].Similarity >= 85);
    }

    [Fact]
    public void FindSimilarImages_DissimilarHash_ReturnsEmpty()
    {
        // Arrange - 완전히 다른 해시
        var files = new List<FileEntry>
        {
            new() { FilePath = "img1.jpg", FileType = FileType.Image, PerceptualHash = 0x0000000000000000UL },
            new() { FilePath = "img2.jpg", FileType = FileType.Image, PerceptualHash = 0xFFFFFFFFFFFFFFFFUL }
        };

        // Act
        var groups = _detector.FindSimilarImages(files, 85);

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void FindSimilarImages_NonImageFiles_Excluded()
    {
        // Arrange
        ulong hash = 0x123456789ABCDEF0UL;
        var files = new List<FileEntry>
        {
            new() { FilePath = "img1.jpg", FileType = FileType.Image, PerceptualHash = hash },
            new() { FilePath = "video.mp4", FileType = FileType.Video, PerceptualHash = hash },
            new() { FilePath = "img2.jpg", FileType = FileType.Image, PerceptualHash = hash }
        };

        // Act
        var groups = _detector.FindSimilarImages(files, 85);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Files.Count);
        Assert.All(groups[0].Files, f => Assert.Equal(FileType.Image, f.FileType));
    }

    [Fact]
    public void FindSimilarVideos_SimilarHash_ReturnsGroup()
    {
        // Arrange
        ulong hash = 0x123456789ABCDEF0UL;
        var files = new List<FileEntry>
        {
            new() { FilePath = "vid1.mp4", FileType = FileType.Video, PerceptualHash = hash },
            new() { FilePath = "vid2.mp4", FileType = FileType.Video, PerceptualHash = hash }
        };

        // Act
        var groups = _detector.FindSimilarVideos(files, 85);

        // Assert
        Assert.Single(groups);
        Assert.Equal(DuplicateType.SimilarVideo, groups[0].Type);
    }

    [Fact]
    public void FindSimilarAudio_SameFingerprint_ReturnsGroup()
    {
        // Arrange
        ulong fingerprint = 0x123456789ABCDEF0UL;
        var files = new List<FileEntry>
        {
            new() { FilePath = "audio1.mp3", FileType = FileType.Audio, AudioFingerprint = fingerprint },
            new() { FilePath = "audio2.mp3", FileType = FileType.Audio, AudioFingerprint = fingerprint }
        };

        // Act
        var groups = _detector.FindSimilarAudio(files, 85);

        // Assert
        Assert.Single(groups);
        Assert.Equal(DuplicateType.SimilarAudio, groups[0].Type);
    }

    [Fact]
    public void FindSimilarAudio_ThresholdOver100_ReturnsEmpty()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "audio1.mp3", FileType = FileType.Audio, AudioFingerprint = 123UL },
            new() { FilePath = "audio2.mp3", FileType = FileType.Audio, AudioFingerprint = 123UL }
        };

        // Act
        var groups = _detector.FindSimilarAudio(files, 101);

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void FindSimilarImages_NullPerceptualHash_Excluded()
    {
        // Arrange
        var files = new List<FileEntry>
        {
            new() { FilePath = "img1.jpg", FileType = FileType.Image, PerceptualHash = 123UL },
            new() { FilePath = "img2.jpg", FileType = FileType.Image, PerceptualHash = null },
            new() { FilePath = "img3.jpg", FileType = FileType.Image, PerceptualHash = 123UL }
        };

        // Act
        var groups = _detector.FindSimilarImages(files, 85);

        // Assert
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Files.Count);
    }
}
