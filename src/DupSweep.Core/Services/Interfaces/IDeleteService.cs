namespace DupSweep.Core.Services.Interfaces;

public interface IDeleteService
{
    Task MoveToTrashAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken);
    Task DeletePermanentlyAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken);
}
