using DupSweep.Core.Models;
using DupSweep.Tests.TestUtilities;

namespace DupSweep.Tests.Models;

public class FileEntryTests : IDisposable
{
    private readonly TestFileGenerator _fileGenerator;

    public FileEntryTests()
    {
        _fileGenerator = new TestFileGenerator();
    }

    [Fact]
    public void FromPath_ValidFile_ReturnsCorrectEntry()
    {
        // Arrange
        var path = _fileGenerator.CreateTextFile("test.txt", 1024, "Hello World");

        // Act
        var entry = FileEntry.FromPath(path);

        // Assert
        Assert.Equal(path, entry.FilePath);
        Assert.Equal("test.txt", entry.FileName);
        Assert.Equal(".txt", entry.Extension);
        Assert.True(entry.Size > 0);
        Assert.Equal(FileType.Other, entry.FileType);
    }

    [Theory]
    [InlineData(".jpg", FileType.Image)]
    [InlineData(".jpeg", FileType.Image)]
    [InlineData(".png", FileType.Image)]
    [InlineData(".gif", FileType.Image)]
    [InlineData(".bmp", FileType.Image)]
    [InlineData(".webp", FileType.Image)]
    [InlineData(".mp4", FileType.Video)]
    [InlineData(".avi", FileType.Video)]
    [InlineData(".mkv", FileType.Video)]
    [InlineData(".mov", FileType.Video)]
    [InlineData(".mp3", FileType.Audio)]
    [InlineData(".wav", FileType.Audio)]
    [InlineData(".flac", FileType.Audio)]
    [InlineData(".txt", FileType.Other)]
    [InlineData(".pdf", FileType.Other)]
    public void FromPath_DifferentExtensions_ReturnsCorrectFileType(string extension, FileType expectedType)
    {
        // Arrange
        var path = _fileGenerator.CreateTextFile($"test{extension}", 100);

        // Act
        var entry = FileEntry.FromPath(path);

        // Assert
        Assert.Equal(expectedType, entry.FileType);
    }

    [Fact]
    public void FromPath_UppercaseExtension_ReturnsCorrectFileType()
    {
        // Arrange
        var path = _fileGenerator.CreateTextFile("test.JPG", 100);

        // Act
        var entry = FileEntry.FromPath(path);

        // Assert
        Assert.Equal(FileType.Image, entry.FileType);
        Assert.Equal(".jpg", entry.Extension);
    }

    [Fact]
    public void FromPath_CapturesFileMetadata()
    {
        // Arrange
        var path = _fileGenerator.CreateTextFile("metadata.txt", 500);
        var fileInfo = new FileInfo(path);

        // Act
        var entry = FileEntry.FromPath(path);

        // Assert
        Assert.Equal(fileInfo.DirectoryName, entry.Directory);
        Assert.Equal(fileInfo.Length, entry.Size);
        Assert.Equal(fileInfo.CreationTime, entry.CreatedDate);
        Assert.Equal(fileInfo.LastWriteTime, entry.ModifiedDate);
    }

    [Fact]
    public void FileEntry_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var entry = new FileEntry();

        // Assert
        Assert.Equal(string.Empty, entry.FilePath);
        Assert.Equal(string.Empty, entry.FileName);
        Assert.Equal(string.Empty, entry.Directory);
        Assert.Equal(string.Empty, entry.Extension);
        Assert.Equal(0, entry.Size);
        Assert.Equal(0, entry.Width);
        Assert.Equal(0, entry.Height);
        Assert.Null(entry.QuickHash);
        Assert.Null(entry.FullHash);
        Assert.Null(entry.PerceptualHash);
        Assert.Null(entry.ThumbnailData);
    }

    [Fact]
    public void FileEntry_HashProperties_CanBeSet()
    {
        // Arrange
        var entry = new FileEntry();

        // Act
        entry.QuickHash = "abc123";
        entry.FullHash = "def456";
        entry.PerceptualHash = 0x123456789ABCDEF0UL;

        // Assert
        Assert.Equal("abc123", entry.QuickHash);
        Assert.Equal("def456", entry.FullHash);
        Assert.Equal(0x123456789ABCDEF0UL, entry.PerceptualHash);
    }

    public void Dispose()
    {
        _fileGenerator.Dispose();
    }
}
