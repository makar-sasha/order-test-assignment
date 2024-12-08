namespace Order.Core.Files;

public interface IWebFile
{
    Task Save(string destinationPath, CancellationToken cancellationToken);
    SaveResult Result();
}