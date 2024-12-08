namespace Order.Core.Files;

public record SaveResult(
    SaveStatus Status, 
    long OrderId, 
    string LocalPath,
    string Url,
    string? ErrorMessage = null);