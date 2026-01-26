using DupSweep.Core.Models;

namespace DupSweep.Core.Algorithms;

public class FileScanner
{
    public IEnumerable<FileEntry> Scan(ScanConfig config, Action<string>? onFileDiscovered, CancellationToken cancellationToken, ManualResetEventSlim? pauseEvent)
    {
        var extensions = new HashSet<string>(
            config.GetSupportedExtensions().Select(e => e.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = config.RecursiveScan,
            IgnoreInaccessible = true,
            AttributesToSkip = GetAttributesToSkip(config)
        };

        var directories = config.Directories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", options);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pauseEvent?.Wait(cancellationToken);

                onFileDiscovered?.Invoke(filePath);

                FileInfo fileInfo;
                try
                {
                    fileInfo = new FileInfo(filePath);
                }
                catch
                {
                    continue;
                }

                if (fileInfo.Length < config.MinFileSize || fileInfo.Length > config.MaxFileSize)
                {
                    continue;
                }

                var extension = fileInfo.Extension.ToLowerInvariant();
                if (extensions.Count > 0 && !extensions.Contains(extension))
                {
                    continue;
                }

                yield return FileEntry.FromPath(filePath);
            }
        }
    }

    private static FileAttributes GetAttributesToSkip(ScanConfig config)
    {
        var attributes = FileAttributes.System;
        if (!config.IncludeHiddenFiles)
        {
            attributes |= FileAttributes.Hidden;
        }

        if (!config.FollowSymlinks)
        {
            attributes |= FileAttributes.ReparsePoint;
        }

        return attributes;
    }
}
