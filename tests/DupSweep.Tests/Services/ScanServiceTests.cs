using DupSweep.Core.Logging;
using DupSweep.Core.Models;
using DupSweep.Core.Processors;
using DupSweep.Core.Services;
using DupSweep.Core.Services.Interfaces;
using DupSweep.Tests.TestUtilities;
using Moq;

namespace DupSweep.Tests.Services;

public class ScanServiceTests : IDisposable
{
    private readonly TestFileGenerator _fileGenerator;
    private readonly Mock<IHashService> _hashServiceMock;
    private readonly Mock<IImageProcessor> _imageProcessorMock;
    private readonly Mock<IVideoProcessor> _videoProcessorMock;
    private readonly Mock<IAppLogger> _loggerMock;
    private readonly ScanService _scanService;

    public ScanServiceTests()
    {
        _fileGenerator = new TestFileGenerator();
        _hashServiceMock = new Mock<IHashService>();
        _imageProcessorMock = new Mock<IImageProcessor>();
        _videoProcessorMock = new Mock<IVideoProcessor>();
        _loggerMock = new Mock<IAppLogger>();

        _scanService = new ScanService(
            _hashServiceMock.Object,
            _imageProcessorMock.Object,
            _videoProcessorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task StartScanAsync_EmptyDirectory_ReturnsSuccessfulResult()
    {
        // Arrange
        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            ScanVideos = false,
            UseImageSimilarity = false,
            UseVideoSimilarity = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var result = await _scanService.StartScanAsync(config, progress);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.TotalFilesScanned);
        Assert.Empty(result.DuplicateGroups);
    }

    [Fact]
    public async Task StartScanAsync_WithFiles_ScansAllFiles()
    {
        // Arrange
        _fileGenerator.CreateTextFile("file1.jpg", 1000);
        _fileGenerator.CreateTextFile("file2.jpg", 1000);

        _hashServiceMock.Setup(h => h.ComputeQuickHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("unique_hash");
        _hashServiceMock.Setup(h => h.ComputeFullHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("unique_full_hash");

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            ScanVideos = false,
            UseImageSimilarity = false,
            UseVideoSimilarity = false,
            UseHashComparison = false,
            UseSizeComparison = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var result = await _scanService.StartScanAsync(config, progress);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.TotalFilesScanned);
    }

    [Fact]
    public async Task StartScanAsync_DuplicateFiles_FindsDuplicates()
    {
        // Arrange
        var duplicates = _fileGenerator.CreateDuplicateFiles("dup", 3, 1000, ".jpg");

        _hashServiceMock.Setup(h => h.ComputeQuickHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("same_quick_hash");
        _hashServiceMock.Setup(h => h.ComputeFullHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("same_full_hash");

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            ScanVideos = false,
            UseImageSimilarity = false,
            UseVideoSimilarity = false,
            UseHashComparison = true,
            UseSizeComparison = true
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var result = await _scanService.StartScanAsync(config, progress);

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.Single(result.DuplicateGroups);
        Assert.Equal(3, result.DuplicateGroups[0].Files.Count);
    }

    [Fact]
    public async Task StartScanAsync_WhileRunning_ThrowsException()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _fileGenerator.CreateTextFile($"file{i}.jpg", 1000);
        }

        _hashServiceMock.Setup(h => h.ComputeQuickHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                return "hash";
            });

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            UseHashComparison = true,
            UseSizeComparison = true,
            UseImageSimilarity = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var firstScan = _scanService.StartScanAsync(config, progress);
        await Task.Delay(50); // 스캔이 시작될 때까지 대기

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _scanService.StartScanAsync(config, progress));

        _scanService.Cancel();
        try { await firstScan; } catch { }
    }

    [Fact]
    public void IsRunning_InitialState_IsFalse()
    {
        // Assert
        Assert.False(_scanService.IsRunning);
    }

    [Fact]
    public void IsPaused_InitialState_IsFalse()
    {
        // Assert
        Assert.False(_scanService.IsPaused);
    }

    [Fact]
    public async Task Cancel_DuringScan_StopsEarly()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _fileGenerator.CreateTextFile($"file{i}.jpg", 1000);
        }

        _hashServiceMock.Setup(h => h.ComputeQuickHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(50, ct);
                return "hash";
            });

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            UseHashComparison = true,
            UseSizeComparison = true,
            UseImageSimilarity = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var scanTask = _scanService.StartScanAsync(config, progress);
        await Task.Delay(100);
        _scanService.Cancel();
        var result = await scanTask;

        // Assert
        Assert.False(result.IsSuccessful);
        Assert.Contains("cancelled", result.ErrorMessage?.ToLower() ?? "");
    }

    [Fact]
    public async Task Pause_DuringScan_PausesScan()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _fileGenerator.CreateTextFile($"file{i}.jpg", 1000);
        }

        // 해시 계산에 지연을 줘서 스캔이 오래 걸리도록 함
        _hashServiceMock.Setup(h => h.ComputeQuickHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                return "hash";
            });

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            UseHashComparison = true,
            UseSizeComparison = true,
            UseImageSimilarity = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var scanTask = _scanService.StartScanAsync(config, progress);
        await Task.Delay(150); // 스캔이 해싱 단계에 진입할 때까지 대기
        _scanService.Pause();

        // Assert
        Assert.True(_scanService.IsPaused);

        // Cleanup
        _scanService.Resume();
        _scanService.Cancel();
        try { await scanTask; } catch { }
    }

    [Fact]
    public async Task Resume_AfterPause_ResumesScan()
    {
        // Arrange
        for (int i = 0; i < 50; i++)
        {
            _fileGenerator.CreateTextFile($"file{i}.jpg", 1000);
        }

        _hashServiceMock.Setup(h => h.ComputeQuickHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                await Task.Delay(50, ct);
                return "hash";
            });

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            UseHashComparison = true,
            UseSizeComparison = true,
            UseImageSimilarity = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var scanTask = _scanService.StartScanAsync(config, progress);
        await Task.Delay(100);
        _scanService.Pause();
        await Task.Delay(50); // Pause 상태 확인을 위한 대기
        var wasPaused = _scanService.IsPaused;
        _scanService.Resume();
        _scanService.Cancel(); // 테스트 빠르게 종료

        try { await scanTask; } catch { }

        // Assert
        Assert.True(wasPaused);
        Assert.False(_scanService.IsPaused);
    }

    [Fact]
    public void Pause_WhenNotRunning_DoesNothing()
    {
        // Act & Assert (should not throw)
        _scanService.Pause();
        Assert.False(_scanService.IsPaused);
    }

    [Fact]
    public void Resume_WhenNotRunning_DoesNothing()
    {
        // Act & Assert (should not throw)
        _scanService.Resume();
    }

    [Fact]
    public void Cancel_WhenNotRunning_DoesNothing()
    {
        // Act & Assert (should not throw)
        _scanService.Cancel();
    }

    [Fact]
    public async Task StartScanAsync_ReportsProgress()
    {
        // Arrange
        _fileGenerator.CreateTextFile("file1.jpg", 1000);

        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            UseHashComparison = false,
            UseSizeComparison = false,
            UseImageSimilarity = false
        };

        // Act
        var result = await _scanService.StartScanAsync(config, progress);
        await Task.Delay(100); // Progress 이벤트가 처리될 시간 허용

        // Assert
        Assert.True(result.IsSuccessful);
        Assert.NotEmpty(progressReports);
    }

    [Fact]
    public async Task StartScanAsync_SetsStartAndEndTime()
    {
        // Arrange
        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            UseHashComparison = false,
            UseImageSimilarity = false
        };

        var progress = new Progress<ScanProgress>();
        var beforeScan = DateTime.Now;

        // Act
        var result = await _scanService.StartScanAsync(config, progress);
        var afterScan = DateTime.Now;

        // Assert
        Assert.True(result.StartTime >= beforeScan);
        Assert.True(result.EndTime <= afterScan);
        Assert.True(result.EndTime >= result.StartTime);
    }

    [Fact]
    public async Task StartScanAsync_ConfigIsStored()
    {
        // Arrange
        var config = new ScanConfig
        {
            Directories = new List<string> { _fileGenerator.TestRootPath },
            ScanImages = true,
            ImageSimilarityThreshold = 95,
            UseImageSimilarity = false
        };

        var progress = new Progress<ScanProgress>();

        // Act
        var result = await _scanService.StartScanAsync(config, progress);

        // Assert
        Assert.Equal(config, result.Config);
    }

    public void Dispose()
    {
        _fileGenerator.Dispose();
    }
}
