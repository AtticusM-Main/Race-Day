using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Infrastructure;

public class FakeImageStorage : IImageStorage
{
    public ConcurrentDictionary<string, long> Stored { get; } = new();
    public ConcurrentBag<string> Deleted { get; } = new();
    public bool Unavailable { get; set; }

    public async Task<string> UploadAsync(Stream content, string folder, string extension, string contentType, CancellationToken ct = default)
    {
        if (Unavailable) throw new ImageStorageUnavailableException("Cloud storage is not reachable right now.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var url = $"https://fake.blob.local/raceday-images/{folder}/{Guid.NewGuid():N}{extension}";
        Stored[url] = buffer.Length;
        return url;
    }

    public Task DeleteAsync(string? url, CancellationToken ct = default)
    {
        if (url is not null) Deleted.Add(url);
        return Task.CompletedTask;
    }
}
