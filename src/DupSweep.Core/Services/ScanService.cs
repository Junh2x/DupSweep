using System.Diagnostics;
using DupSweep.Core.Algorithms;
using DupSweep.Core.Models;
using DupSweep.Core.Processors;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.Core.Services;

public class ScanService : IScanService
{
    private readonly IHashService _hashService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IVideoProcessor _videoProcessor;
    private readonly IAudioProcessor _audioProcessor;
    private readonly IThumbnailCache? _thumbnailCache;
    private readonly FileScanner _fileScanner = new();
    private readonly DuplicateDetector _duplicateDetector = new();
    private readonly object _stateLock = new();

    private CancellationTokenSource? _cts;
    private ManualResetEventSlim _pauseEvent = new(true);
    private IProgress<ScanProgress>? _progress;
    private bool _isRunning;
    private bool _isPaused;

    public ScanService(IHashService hashService, IImageProcessor imageProcessor, IVideoProcessor videoProcessor, IAudioProcessor audioProcessor, IThumbnailCache? thumbnailCache = null)
    {
        _hashService = hashService;
        _imageProcessor = imageProcessor;
        _videoProcessor = videoProcessor;
        _audioProcessor = audioProcessor;
        _thumbnailCache = thumbnailCache;
    }

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;

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

    public void Pause()
    {
        if (!_isRunning)
        {
            return;
        }

        _isPaused = true;
        _pauseEvent.Reset();
    }

    public void Resume()
    {
        if (!_isRunning)
        {
            return;
        }

        _isPaused = false;
        _pauseEvent.Set();
    }

    public void Cancel()
    {
        if (!_isRunning)
        {
            return;
        }

        _cts?.Cancel();
    }

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
            var needsHashComparison = config.UseHashComparison;

            List<FileEntry> quickGroups = new();

            if (needsHashComparison)
            {
                ReportProgress(ScanPhase.Hashing, 0, scannedFiles.Count, string.Empty, 0, 0, stopwatch);

                var candidates = scannedFiles
                    .GroupBy(f => f.Size)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g)
                    .ToList();

                int processed = 0;
                var parallelism = Math.Max(1, config.ParallelThreads);
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

                quickGroups = candidates
                    .Where(f => !string.IsNullOrWhiteSpace(f.QuickHash))
                    .GroupBy(f => (f.Size, f.QuickHash))
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g)
                    .ToList();

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

                duplicateGroups.AddRange(_duplicateDetector.FindExactMatches(quickGroups, config));
            }

            if (config.UseImageSimilarity && config.ScanImages)
            {
                var excluded = new HashSet<string>(
                    duplicateGroups.SelectMany(g => g.Files).Select(f => f.FilePath),
                    StringComparer.OrdinalIgnoreCase);

                var images = scannedFiles
                    .Where(f => f.FileType == FileType.Image && !excluded.Contains(f.FilePath))
                    .ToList();
                int processedImages = 0;
                foreach (var image in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _pauseEvent.Wait(cancellationToken);

                    image.PerceptualHash = await _imageProcessor.ComputePerceptualHashAsync(image.FilePath, config, cancellationToken);
                    processedImages++;
                    ReportProgress(ScanPhase.Comparing, processedImages, images.Count, image.FilePath, duplicateGroups.Count, duplicateGroups.Sum(g => g.PotentialSavings), stopwatch);
                }

                duplicateGroups.AddRange(_duplicateDetector.FindSimilarImages(images, config.ImageSimilarityThreshold));
            }

            if (config.UseVideoSimilarity && config.ScanVideos)
            {
                var excluded = new HashSet<string>(
                    duplicateGroups.SelectMany(g => g.Files).Select(f => f.FilePath),
                    StringComparer.OrdinalIgnoreCase);

                var videos = scannedFiles
                    .Where(f => f.FileType == FileType.Video && !excluded.Contains(f.FilePath))
                    .ToList();

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
            }

            if (config.UseAudioSimilarity && config.ScanAudio)
            {
                var excluded = new HashSet<string>(
                    duplicateGroups.SelectMany(g => g.Files).Select(f => f.FilePath),
                    StringComparer.OrdinalIgnoreCase);

                var audios = scannedFiles
                    .Where(f => f.FileType == FileType.Audio && !excluded.Contains(f.FilePath))
                    .ToList();

                int processedAudio = 0;
                foreach (var audio in audios)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _pauseEvent.Wait(cancellationToken);

                    audio.AudioFingerprint = await _audioProcessor.ComputeFingerprintAsync(audio.FilePath, config, cancellationToken);
                    processedAudio++;
                    ReportProgress(ScanPhase.Comparing, processedAudio, audios.Count, audio.FilePath, duplicateGroups.Count, duplicateGroups.Sum(g => g.PotentialSavings), stopwatch);
                }

                duplicateGroups.AddRange(_duplicateDetector.FindSimilarAudio(audios, config.AudioSimilarityThreshold));
            }

            await GenerateThumbnailsAsync(duplicateGroups, config, cancellationToken, stopwatch);

            var potentialSavings = duplicateGroups.Sum(g => g.PotentialSavings);
            ReportProgress(ScanPhase.Comparing, scannedFiles.Count, scannedFiles.Count, string.Empty, duplicateGroups.Count, potentialSavings, stopwatch);

            result.DuplicateGroups = duplicateGroups;
            result.TotalFilesScanned = scannedFiles.Count;
            result.IsSuccessful = true;
            result.EndTime = DateTime.Now;

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

    private async Task GenerateThumbnailsAsync(IEnumerable<DuplicateGroup> groups, ScanConfig config, CancellationToken cancellationToken, Stopwatch stopwatch)
    {
        var files = groups.SelectMany(g => g.Files)
            .GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        int processed = 0;
        var groupCount = groups.Count();
        var potentialSavings = groups.Sum(g => g.PotentialSavings);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            if (file.FileType != FileType.Image && file.FileType != FileType.Video)
            {
                processed++;
                continue;
            }

            byte[]? thumbnail = null;
            if (_thumbnailCache != null)
            {
                thumbnail = await _thumbnailCache.TryGetAsync(file.FilePath, file.Size, file.ModifiedDate, cancellationToken);
            }

            if (thumbnail == null)
            {
                if (file.FileType == FileType.Image)
                {
                    thumbnail = await _imageProcessor.CreateThumbnailAsync(file.FilePath, config, cancellationToken);
                }
                else if (file.FileType == FileType.Video)
                {
                    thumbnail = await _videoProcessor.CreateThumbnailAsync(file.FilePath, config, cancellationToken);
                }

                if (thumbnail != null && _thumbnailCache != null)
                {
                    await _thumbnailCache.SaveAsync(file.FilePath, file.Size, file.ModifiedDate, thumbnail, cancellationToken);
                }
            }

            file.ThumbnailData = thumbnail;
            processed++;
            ReportProgress(ScanPhase.GeneratingThumbnails, processed, files.Count, file.FilePath, groupCount, potentialSavings, stopwatch);
        }
    }
}
