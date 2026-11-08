namespace RaceDay.Api.Services;

public static class ImageUploadValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    /// <summary>Returns an error message, or null when the file is an acceptable image.</summary>
    public static string? Validate(IFormFile? file, out string extension, out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;

        if (file is null || file.Length == 0) return "Choose an image to upload.";
        if (file.Length > MaxBytes) return "The image must be 5 MB or smaller.";

        extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var expectedType)) return "Only JPG, PNG or WebP images are allowed.";
        if (!string.Equals(file.ContentType, expectedType, StringComparison.OrdinalIgnoreCase)) return "The file type does not match its extension.";

        contentType = expectedType;
        return null;
    }
}
