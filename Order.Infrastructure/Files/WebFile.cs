using System.Text;
using Order.Core.Files;
using MurmurHash.Net;
using Order.Core.Orders;
using Polly;
using Polly.Retry;

namespace Order.Infrastructure.Files;

public class WebFile(HttpClient httpClient, FileLink fileLink) : IWebFile
{
    private SaveResult _saveResult = new(SaveStatus.NotStarted, 0, string.Empty, string.Empty);
    
    private readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        );
    public async Task Save(string destinationPath, CancellationToken cancellationToken)
    {
        if (!ValidateUrl(fileLink.Url))
        {
            _saveResult = new SaveResult(SaveStatus.Failure, fileLink.OrderId, destinationPath, fileLink.Url, "The URL is invalid.");
            return;
        }
        if (!ValidateDestinationPath(destinationPath))
        {
            _saveResult = new SaveResult(SaveStatus.Failure, fileLink.OrderId, destinationPath, fileLink.Url, "The path is invalid.");
            return;
        }
        
        _saveResult = new SaveResult(SaveStatus.InProgress, fileLink.OrderId, destinationPath, fileLink.Url);
        
        try
        {
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var response = await httpClient.GetAsync(fileLink.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound) return response;
                _saveResult = new SaveResult(
                    SaveStatus.Failure,
                    fileLink.OrderId,
                    destinationPath,
                    fileLink.Url,
                    "The file was not found on the server."
                );
                return null;

            });

            if (response == null) return;

            if (response.Content.Headers.ContentLength == 0)
            {
                _saveResult = _saveResult with { Status = SaveStatus.Failure, ErrorMessage = "The file is empty." };
                return;
            }

            var fileName = GetFileNameFromResponse(response) ?? "default.dat";
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var fileExtension = Path.GetExtension(fileName);
            var urlHash = GenerateMurmurHash(fileLink.Url);

            var uniqueFileName = $"{fileNameWithoutExtension}_{urlHash}{fileExtension}";
            var fullPath = Path.Combine(destinationPath, uniqueFileName);

            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream, cancellationToken);

            _saveResult = _saveResult with { Status = SaveStatus.Success, LocalPath = fullPath };
        }
        catch (HttpRequestException ex)
        {
            _saveResult = _saveResult with { Status = SaveStatus.Failure, ErrorMessage = ex.Message };
        }
        catch (TaskCanceledException ex)
        {
            _saveResult = _saveResult with { Status = SaveStatus.Failure, ErrorMessage = "Operation timed out: " + ex.Message };
        }
        catch (Exception ex)
        {
            _saveResult = _saveResult with { Status = SaveStatus.Failure, ErrorMessage = ex.Message };
        }
    }
    
    private static string GenerateMurmurHash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hash = MurmurHash3.Hash32(inputBytes, seed: 0);
        return hash.ToString("X");
    }

    public SaveResult Result()
    {
        return _saveResult;
    }
    
    private bool ValidateDestinationPath(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) return false;

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var fullDestinationPath = Path.GetFullPath(destinationPath);

        if (!fullDestinationPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase)) return false;
        Directory.CreateDirectory(destinationPath);
        return true;
    }
    
    private bool ValidateUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute);
    }
    
    private static string? GetFileNameFromResponse(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentDisposition?.FileName != null)
        {
            return response.Content.Headers.ContentDisposition.FileName.Trim('"');
        }

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri == null) return null;
        var fileName = Path.GetFileName(finalUri.LocalPath);
        return !string.IsNullOrEmpty(fileName) ? fileName : null;
    }
}