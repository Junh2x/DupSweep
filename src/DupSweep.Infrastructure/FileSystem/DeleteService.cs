using DupSweep.Core.Services.Interfaces;
using Microsoft.VisualBasic.FileIO;

namespace DupSweep.Infrastructure.FileSystem;

public class DeleteService : IDeleteService
{
    public Task MoveToTrashAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(filePath))
                {
                    continue;
                }

                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
        }, cancellationToken);
    }

    public Task DeletePermanentlyAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(filePath))
                {
                    continue;
                }

                File.Delete(filePath);
            }
        }, cancellationToken);
    }
}
