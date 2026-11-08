namespace RaceDay.Web.Services;

public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>Returns a user-facing message when the upload can't be accepted, otherwise null.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Choose an image to upload.";
        if (file.Length > MaxBytes) return "The image must be 5 MB or smaller.";
        if (!AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) return "Only JPG, PNG or WebP images are allowed.";

        return null;
    }
}
