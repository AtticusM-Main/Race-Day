using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

public class BlobStorageSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "raceday-images";
}

public class ImageStorageUnavailableException : Exception
{
    public ImageStorageUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

public interface IImageStorage
{
    /// <summary>Stores the image and returns the public URL it can be displayed from.</summary>
    Task<string> UploadAsync(Stream content, string folder, string extension, string contentType, CancellationToken ct = default);

    /// <summary>Removes a previously uploaded image. Missing blobs are ignored.</summary>
    Task DeleteAsync(string? url, CancellationToken ct = default);
}

/// <summary>
/// Stores images in an Azure Blob Storage container. The container is created on first use
/// with public read access on blobs, so the stored URL can be used directly in an img tag.
/// </summary>
public class AzureBlobImageStorage : IImageStorage
{
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<AzureBlobImageStorage> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private BlobContainerClient? _container;

    public AzureBlobImageStorage(IOptions<BlobStorageSettings> settings, ILogger<AzureBlobImageStorage> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(Stream content, string folder, string extension, string contentType, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient($"{folder}/{Guid.NewGuid():N}{extension}");

        try
        {
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "public, max-age=86400" }
            }, ct);
        }
        catch (Exception ex) when (ex is Azure.RequestFailedException or HttpRequestException)
        {
            _logger.LogError(ex, "Uploading an image to Azure Blob Storage failed.");
            throw new ImageStorageUnavailableException("The image could not be saved to cloud storage.", ex);
        }

        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        try
        {
            var container = await GetContainerAsync(ct);
            var prefix = $"/{container.Name}/";
            var index = uri.AbsolutePath.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0) return;

            var blobName = Uri.UnescapeDataString(uri.AbsolutePath[(index + prefix.Length)..]);
            await container.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is Azure.RequestFailedException or HttpRequestException or ImageStorageUnavailableException)
        {
            _logger.LogWarning(ex, "Could not delete the old image {Url}.", url);
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        if (_container is not null) return _container;

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            throw new ImageStorageUnavailableException("Image storage is not configured. Set BlobStorage:ConnectionString.");
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_container is not null) return _container;

            var container = new BlobContainerClient(_settings.ConnectionString, _settings.ContainerName);
            try
            {
                await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
            }
            catch (Azure.RequestFailedException ex) when (ex.ErrorCode == "PublicAccessNotPermitted")
            {
                await container.CreateIfNotExistsAsync(cancellationToken: ct);
            }
            catch (Exception ex) when (ex is Azure.RequestFailedException or HttpRequestException or AggregateException)
            {
                _logger.LogError(ex, "Could not reach Azure Blob Storage.");
                throw new ImageStorageUnavailableException("Cloud storage is not reachable right now.", ex);
            }

            _container = container;
            return container;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
