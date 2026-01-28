using DupSweep.Core.Algorithms;
using DupSweep.Core.Models;
using DupSweep.Tests.TestUtilities;

namespace DupSweep.Tests.Algorithms;

public class FileScannerTests : IDisposable
{
    private readonly TestFileGenerator _fileGenerator;
    private readonly FileScanner _scanner;

    public FileScannerTests()
    {
        _fileGenerator = new TestFileGenerator();
        _scanner = new FileScanner();
    }

    [Fact]
    public void Scan_ValidDirectory_ReturnsFiles()
    {
        // Arrange
        _fileGenerator.CreateTextFile("test1.jpg", 1000);
        _fileGenerator.CreateTextFile("test2.png", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            ScanVideos = false
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Scan_FiltersByExtension()
    {
        // Arrange
        _fileGenerator.CreateTextFile("image.jpg", 1000);
        _fileGenerator.CreateTextFile("video.mp4", 1000);
        _fileGenerator.CreateTextFile("document.txt", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanAllFiles = false, // 확장자 필터링 활성화
            ScanImages = true,
            ScanVideos = false
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
        Assert.Equal(".jpg", files[0].Extension);
    }

    [Fact]
    public void Scan_RecursiveScan_IncludesSubdirectories()
    {
        // Arrange
        _fileGenerator.CreateTextFile("root.jpg", 1000);
        _fileGenerator.CreateSubDirectory("subdir");
        _fileGenerator.CreateTextFile("subdir/nested.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            RecursiveScan = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Scan_NonRecursive_ExcludesSubdirectories()
    {
        // Arrange
        _fileGenerator.CreateTextFile("root.jpg", 1000);
        _fileGenerator.CreateSubDirectory("subdir");
        _fileGenerator.CreateTextFile("subdir/nested.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            RecursiveScan = false
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
        Assert.Contains("root.jpg", files[0].FileName);
    }

    [Fact]
    public void Scan_MinFileSize_FiltersSmallFiles()
    {
        // Arrange
        _fileGenerator.CreateTextFile("small.jpg", 100);
        _fileGenerator.CreateTextFile("large.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            MinFileSize = 500
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
        Assert.Contains("large.jpg", files[0].FileName);
    }

    [Fact]
    public void Scan_MaxFileSize_FiltersLargeFiles()
    {
        // Arrange
        _fileGenerator.CreateTextFile("small.jpg", 100);
        _fileGenerator.CreateTextFile("large.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            MaxFileSize = 500
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
        Assert.Contains("small.jpg", files[0].FileName);
    }

    [Fact]
    public void Scan_CancellationRequested_StopsEarly()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _fileGenerator.CreateTextFile($"file{i}.jpg", 100);
        }

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() =>
            _scanner.Scan(config, null, cts.Token, null).ToList());
    }

    [Fact]
    public void Scan_NonExistentDirectory_ReturnsEmpty()
    {
        // Arrange
        var config = new ScanConfig
        {
            Directories = new List<string> { @"C:\NonExistentDirectory12345" },
            ScanImages = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public void Scan_MultipleDirectories_ScansAll()
    {
        // Arrange
        var dir1 = _fileGenerator.CreateSubDirectory("dir1");
        var dir2 = _fileGenerator.CreateSubDirectory("dir2");

        _fileGenerator.CreateTextFile("dir1/file1.jpg", 1000);
        _fileGenerator.CreateTextFile("dir2/file2.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { dir1, dir2 },
            ScanImages = true,
            RecursiveScan = false
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Scan_OnFileDiscovered_CallbackInvoked()
    {
        // Arrange
        _fileGenerator.CreateTextFile("test.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true
        };

        var discoveredFiles = new List<string>();

        // Act
        _scanner.Scan(config, path => discoveredFiles.Add(path), CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(discoveredFiles);
    }

    [Fact]
    public void Scan_DuplicateDirectories_ProcessedOnce()
    {
        // Arrange
        _fileGenerator.CreateTextFile("test.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string>
            {
                _fileGenerator.TestRootPath,
                _fileGenerator.TestRootPath // 중복
            },
            ScanImages = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
    }

    [Fact]
    public void Scan_WhitespaceDirectory_Ignored()
    {
        // Arrange
        _fileGenerator.CreateTextFile("test.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string>
            {
                _fileGenerator.TestRootPath,
                "",
                "   ",
                null!
            },
            ScanImages = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
    }

    [Fact]
    public void Scan_HiddenFile_ExcludedByDefault()
    {
        // Arrange
        _fileGenerator.CreateTextFile("visible.jpg", 1000);
        _fileGenerator.CreateHiddenFile("hidden.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            IncludeHiddenFiles = false
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
        Assert.Contains("visible.jpg", files[0].FileName);
    }

    [Fact]
    public void Scan_IncludeHiddenFiles_IncludesHidden()
    {
        // Arrange
        _fileGenerator.CreateTextFile("visible.jpg", 1000);
        _fileGenerator.CreateHiddenFile("hidden.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            IncludeHiddenFiles = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Scan_ScanBothImagesAndVideos_ReturnsAll()
    {
        // Arrange
        _fileGenerator.CreateTextFile("image.jpg", 1000);
        _fileGenerator.CreateTextFile("video.mp4", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            ScanVideos = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Scan_ReturnsCorrectFileEntry()
    {
        // Arrange
        var path = _fileGenerator.CreateTextFile("test.jpg", 1000);

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true
        };

        // Act
        var files = _scanner.Scan(config, null, CancellationToken.None, null).ToList();

        // Assert
        Assert.Single(files);
        var entry = files[0];
        Assert.Equal(path, entry.FilePath);
        Assert.Equal("test.jpg", entry.FileName);
        Assert.Equal(".jpg", entry.Extension);
        Assert.Equal(FileType.Image, entry.FileType);
        Assert.True(entry.Size > 0);
    }

    public void Dispose()
    {
        _fileGenerator.Dispose();
    }
}
