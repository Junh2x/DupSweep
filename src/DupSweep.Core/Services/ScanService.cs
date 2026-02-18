using System.Diagnostics;
using DupSweep.Core.Algorithms;
using DupSweep.Core.Logging;
using DupSweep.Core.Models;
using DupSweep.Core.Processors;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.Core.Services;

/// <summary>
/// 중복 파일 스캔 서비스
/// 파일 탐색, 해시 계산, 중복 탐지 등 전체 스캔 프로세스 관리
/// </summary>
public class ScanService : IScanService
{
    private readonly IHashService _hashService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IVideoProcessor _videoProcessor;
    private readonly IAppLogger _logger;
    private readonly FileScanner _fileScanner = new();
    private readonly DuplicateDetector _duplicateDetector = new();
    private readonly object _stateLock = new();

    // 스캔 상태 관리
    private CancellationTokenSource? _cts;
    private ManualResetEventSlim _pauseEvent = new(true);
    private IProgress<ScanProgress>? _progress;
    private bool _isRunning;
    private bool _isPaused;

    public ScanService(IHashService hashService, IImageProcessor imageProcessor, IVideoProcessor videoProcessor, IAppLogger logger)
    {
        _hashService = hashService;
        _imageProcessor = imageProcessor;
        _videoProcessor = videoProcessor;
        _logger = logger;
    }

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;

    /// <summary>
    /// 스캔 시작 (비동기)
    /// </summary>
    public Task<ScanResult> StartScanAsync(ScanConfig config, IProgress<ScanProgress> progress)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("A scan is already running.");
            }

            _isRunning = true;
            _isPaused = false;
            _pauseEvent = new ManualResetEventSlim(true);
            _cts = new CancellationTokenSource();
            _progress = progress;
        }

        return Task.Run(() => RunScanAsync(config, _cts.Token));
    }

    /// <summary>
    /// 스캔 일시 정지
    /// </summary>
    public void Pause()
    {
        if (!_isRunning)
        {
            return;
        }

        _isPaused = true;
        _pauseEvent.Reset();
    }

    /// <summary>
    /// 스캔 재개
    /// </summary>
    public void Resume()
    {
        if (!_isRunning)
        {
            return;
        }

        _isPaused = false;
        _pauseEvent.Set();
    }

    /// <summary>
    /// 스캔 취소
    /// </summary>
    public void Cancel()
    {
        if (!_isRunning)
        {
            return;
        }

        _cts?.Cancel();
    }

    /// <summary>
    /// 실제 스캔 실행 로직
    /// </summary>
    private async Task<ScanResult> RunScanAsync(ScanConfig config, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult
        {
            StartTime = DateTime.Now,
            Config = config
        };

        try
        {
            ReportProgress(ScanPhase.Initializing, 0, 0, string.Empty, 0, 0, stopwatch);

            // 1단계: 파일 스캔
            var scannedFiles = new List<FileEntry>();
            int scannedCount = 0;

            foreach (var entry in _fileScanner.Scan(config, filePath =>
                     {
                         scannedCount++;
                         ReportProgress(ScanPhase.Scanning, scannedCount, 0, filePath, 0, 0, stopwatch);
                     }, cancellationToken, _pauseEvent))
            {
                scannedFiles.Add(entry);
            }

            var duplicateGroups = new List<DuplicateGroup>();
            var parallelism = Math.Max(1, config.ParallelThreads);

            // 해상도 비교가 필요한 경우 이미지/비디오 해상도 추출
            if (config.UseResolutionComparison)
            {
                _logger.LogDebug("해상도 추출 시작");
                var mediaFiles = scannedFiles.Where(f => f.FileType == FileType.Image || f.FileType == FileType.Video).ToList();
                int processed = 0;
                foreach (var file in mediaFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _pauseEvent.Wait(cancellationToken);

                    try
                    {
                        if (file.FileType == FileType.Image)
                        {
                            var (width, height) = await _imageProcessor.GetImageResolutionAsync(file.FilePath, cancellationToken);
                            file.Width = width;
                            file.Height = height;
                        }
                    }
                    catch { }

                    processed++;
                    if (processed % 50 == 0)
                    {
                        ReportProgress(ScanPhase.Scanning, processed, mediaFiles.Count, file.FilePath, 0, 0, stopwatch);
                    }
                }
                _logger.LogDebug("해상도 추출 완료: {Count}개 파일", mediaFiles.Count);
            }

            // 2단계: 용량 또는 해시 기반 중복 탐지
            if (config.UseSizeComparison || config.UseHashComparison)
            {
                ReportProgress(ScanPhase.Hashing, 0, scannedFiles.Count, string.Empty, 0, 0, stopwatch);

                // 용량/해상도로 후보 그룹화
                IEnumerable<IGrouping<object, FileEntry>> candidateGroups;

                if (config.UseResolutionComparison)
                {
                    candidateGroups = scannedFiles
                        .GroupBy(f => (object)(f.Size, f.Width, f.Height))
                        .Where(g => g.Count() > 1);
                }
                else
                {
                    candidateGroups = scannedFiles
                        .GroupBy(f => (object)f.Size)
                        .Where(g => g.Count() > 1);
                }

                var candidates = candidateGroups.SelectMany(g => g).ToList();
                _logger.LogDebug("용량/해상도 기준 후보 파일: {Count}개", candidates.Count);

                // 날짜 필터 적용
                if (config.MatchCreatedDate)
                {
                    candidates = candidates
                        .GroupBy(f => f.CreatedDate.Date)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g)
                        .ToList();
                }

                if (config.MatchModifiedDate)
                {
                    candidates = candidates
                        .GroupBy(f => f.ModifiedDate.Date)
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g)
                        .ToList();
                }

                // 해시 비교 수행
                if (config.UseHashComparison && candidates.Count > 0)
                {
                    _logger.LogDebug("해시 계산 시작: {Count}개 후보", candidates.Count);
                    int processed = 0;

                    // 빠른 해시(QuickHash) 계산
                    await Parallel.ForEachAsync(
                        candidates,
                        new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken },
                        async (file, ct) =>
                        {
                            _pauseEvent.Wait(ct);

                            try
                            {
                                file.QuickHash = await _hashService.ComputeQuickHashAsync(file.FilePath, ct);
                            }
                            catch
                            {
                                file.QuickHash = null;
                            }

                            var current = Interlocked.Increment(ref processed);
                            if (current % 10 == 0 || current == candidates.Count)
                            {
                                ReportProgress(ScanPhase.Hashing, current, candidates.Count, file.FilePath, 0, 0, stopwatch);
                            }
                        });

                    // 빠른 해시가 일치하는 후보 추출
                    var quickGroups = candidates
                        .Where(f => !string.IsNullOrWhiteSpace(f.QuickHash))
                        .GroupBy(f => (f.Size, f.QuickHash))
                        .Where(g => g.Count() > 1)
                        .SelectMany(g => g)
                        .ToList();

                    // 전체 해시(FullHash) 계산
                    processed = 0;
                    await Parallel.ForEachAsync(
                        quickGroups,
                        new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken },
                        async (file, ct) =>
                        {
                            _pauseEvent.Wait(ct);

                            try
                            {
                                file.FullHash = await _hashService.ComputeFullHashAsync(file.FilePath, ct);
                            }
                            catch
                            {
                                file.FullHash = null;
                            }

                            var current = Interlocked.Increment(ref processed);
                            if (current % 10 == 0 || current == quickGroups.Count)
                            {
                                ReportProgress(ScanPhase.Hashing, current, quickGroups.Count, file.FilePath, 0, 0, stopwatch);
                            }
                        });

                    _logger.LogDebug("해시 계산 완료, 정확한 일치 항목 검색 중");
                    duplicateGroups.AddRange(_duplicateDetector.FindExactMatches(quickGroups, config));
                    _logger.LogDebug("정확한 일치 그룹: {Count}개", duplicateGroups.Count);
                }
                else if (!config.UseHashComparison && candidates.Count > 0)
                {
                    // 해시 비교 없이 용량/해상도/날짜만으로 중복 그룹 생성
                    _logger.LogDebug("해시 없이 용량/해상도 기준 그룹 생성");

                    IEnumerable<List<FileEntry>> groups;

                    if (config.UseResolutionComparison)
                    {
                        groups = candidates
                            .GroupBy(f => (f.Size, f.Width, f.Height))
                            .Where(g => g.Count() > 1)
                            .Select(g => g.ToList());
                    }
                    else
                    {
                        groups = candidates
                            .GroupBy(f => f.Size)
                            .Where(g => g.Count() > 1)
                            .Select(g => g.ToList());
                    }

                    foreach (var group in groups)
                    {
                        duplicateGroups.Add(new DuplicateGroup
                        {
                            Type = DuplicateType.ExactMatch,
                            Similarity = 100,
                            Files = group
                        });
                    }
                }
            }

            // 3단계: 이미지 유사도 비교
            _logger.LogDebug("이미지 유사도 비교: UseImageSimilarity={UseImageSimilarity}, ScanImages={ScanImages}", config.UseImageSimilarity, config.ScanImages);
            if (config.UseImageSimilarity && config.ScanImages)
            {
                _logger.LogDebug("이미지 유사도 비교 시작");
                var excluded = new HashSet<string>(
                    duplicateGroups.SelectMany(g => g.Files).Select(f => f.FilePath),
                    StringComparer.OrdinalIgnoreCase);

                var images = scannedFiles
                    .Where(f => f.FileType == FileType.Image && !excluded.Contains(f.FilePath))
                    .ToList();
                _logger.LogDebug("유사도 비교 대상 이미지: {Count}개", images.Count);
                int processedImages = 0;
                foreach (var image in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _pauseEvent.Wait(cancellationToken);

                    image.PerceptualHash = await _imageProcessor.ComputePerceptualHashAsync(image.FilePath, config, cancellationToken);
                    image.ColorHash = await _imageProcessor.ComputeColorHashAsync(image.FilePath, cancellationToken);
                    processedImages++;
                    ReportProgress(ScanPhase.Comparing, processedImages, images.Count, image.FilePath, duplicateGroups.Count, duplicateGroups.Sum(g => g.PotentialSavings), stopwatch);
                }

                duplicateGroups.AddRange(_duplicateDetector.FindSimilarImages(images, config.ImageSimilarityThreshold));
                _logger.LogDebug("이미지 유사도 비교 완료");
            }

            // 4단계: 비디오 유사도 비교
            _logger.LogDebug("비디오 유사도 비교: UseVideoSimilarity={UseVideoSimilarity}, ScanVideos={ScanVideos}", config.UseVideoSimilarity, config.ScanVideos);
            if (config.UseVideoSimilarity && config.ScanVideos)
            {
                bool hasFfmpeg = !string.IsNullOrWhiteSpace(config.FfmpegPath) && File.Exists(config.FfmpegPath);
                if (!hasFfmpeg)
                {
                    _logger.LogWarning("FFmpeg 미설정으로 비디오 유사도 비교 건너뜀");
                }
                else
                {
                    _logger.LogDebug("비디오 유사도 비교 시작");
                    var excluded = new HashSet<string>(
                        duplicateGroups.SelectMany(g => g.Files).Select(f => f.FilePath),
                        StringComparer.OrdinalIgnoreCase);

                    var videos = scannedFiles
                        .Where(f => f.FileType == FileType.Video && !excluded.Contains(f.FilePath))
                        .ToList();
                    _logger.LogDebug("유사도 비교 대상 비디오: {Count}개", videos.Count);

                    int processedVideos = 0;
                    foreach (var video in videos)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _pauseEvent.Wait(cancellationToken);

                        video.PerceptualHash = await _videoProcessor.ComputePerceptualHashAsync(video.FilePath, config, cancellationToken);
                        processedVideos++;
                        ReportProgress(ScanPhase.Comparing, processedVideos, videos.Count, video.FilePath, duplicateGroups.Count, duplicateGroups.Sum(g => g.PotentialSavings), stopwatch);
                    }

                    duplicateGroups.AddRange(_duplicateDetector.FindSimilarVideos(videos, config.VideoSimilarityThreshold));
                    _logger.LogDebug("비디오 유사도 비교 완료");
                }
            }

            var potentialSavings = duplicateGroups.Sum(g => g.PotentialSavings);
            ReportProgress(ScanPhase.Comparing, scannedFiles.Count, scannedFiles.Count, string.Empty, duplicateGroups.Count, potentialSavings, stopwatch);

            result.DuplicateGroups = duplicateGroups;
            result.TotalFilesScanned = scannedFiles.Count;
            result.IsSuccessful = true;
            result.EndTime = DateTime.Now;

            _logger.LogInformation("스캔 완료 - 그룹: {Groups}개, 파일: {Files}개", duplicateGroups.Count, scannedFiles.Count);
            ReportProgress(ScanPhase.Completed, scannedFiles.Count, scannedFiles.Count, string.Empty, duplicateGroups.Count, potentialSavings, stopwatch);
        }
        catch (OperationCanceledException)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = "Scan cancelled.";
            result.EndTime = DateTime.Now;
            ReportProgress(ScanPhase.Cancelled, 0, 0, string.Empty, 0, 0, stopwatch);
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            _logger.LogError(ex, "스캔 중 오류 발생");
            ReportProgress(ScanPhase.Error, 0, 0, string.Empty, 0, 0, stopwatch);
        }
        finally
        {
            _isRunning = false;
            _isPaused = false;
            _pauseEvent.Set();
            _cts?.Dispose();
            _cts = null;
            _progress = null;
        }

        return result;
    }

    /// <summary>
    /// 진행 상황 보고
    /// </summary>
    private void ReportProgress(ScanPhase phase, int processedFiles, int totalFiles, string currentFile, int duplicateGroups, long potentialSavings, Stopwatch stopwatch)
    {
        _progress?.Report(new ScanProgress
        {
            Phase = phase,
            ProcessedFiles = processedFiles,
            TotalFiles = totalFiles,
            CurrentFile = currentFile,
            DuplicateGroupsFound = duplicateGroups,
            PotentialSavings = potentialSavings,
            ElapsedTime = stopwatch.Elapsed,
            IsPaused = _isPaused,
            IsCancelled = _cts?.IsCancellationRequested ?? false
        });
    }
}
