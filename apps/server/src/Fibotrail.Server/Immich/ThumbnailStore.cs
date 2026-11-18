using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Immich;

/// <summary>An image, ready to send to the browser.</summary>
public sealed record StoredImage(byte[] Bytes, string ContentType);

/// <summary>
/// A photo that the proxy may serve, and the version of its image. The version changes when Immich
/// makes a new thumbnail, such as after a rotation, and a scan reads the change.
/// </summary>
public sealed record ImageKey(Guid AssetId, string Version);

/// <summary>
/// Fetches images from Immich for the browser, so the browser never needs the Immich key. Fetched
/// images are kept in the data folder under their version, so a photo that changes in Immich is
/// fetched again after the next scan. The cache can be deleted at any time and refills on demand.
/// </summary>
public sealed class ThumbnailStore(FibotrailDbContext db, IImmichClient immich, ServerSettings settings)
{
    /// <returns>The photo and its image version, or null when the ID isn't a photo in Fibotrail's database.</returns>
    public async Task<ImageKey?> FindAsync(string id, CancellationToken cancellationToken)
    {
        // Only a well-formed ID that the scanner stored is served. This also keeps the file path safe.
        if (!Guid.TryParseExact(id, "D", out var assetId))
        {
            return null;
        }

        var key = assetId.ToString();
        var photo = await db.Photos
            .Where(photo => photo.Id == key)
            .Select(photo => new { photo.Thumbhash, photo.ImmichUpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        return photo is null ? null : new ImageKey(assetId, VersionOf(photo.Thumbhash, photo.ImmichUpdatedAt));
    }

    /// <returns>The image, or null when Immich no longer has the photo.</returns>
    public async Task<StoredImage?> GetAsync(ImageKey image, ImmichImageSize size, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        var key = image.AssetId.ToString();
        var folder = Path.Combine(settings.ThumbnailDirectory, size == ImmichImageSize.Preview ? "preview" : "thumbnail", key[..2]);
        var path = Path.Combine(folder, $"{key}.{image.Version}");
        if (File.Exists(path))
        {
            return ToImage(await File.ReadAllBytesAsync(path, cancellationToken));
        }

        byte[] bytes;
        try
        {
            bytes = await immich.GetImageAsync(image.AssetId, size, cancellationToken);
        }
        catch (ImmichNotFoundException)
        {
            // The photo was deleted from Immich after the scan.
            return null;
        }

        // Write to a temporary name first, so a request that arrives mid-write never reads half a file.
        Directory.CreateDirectory(folder);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
        File.Move(temporary, path, overwrite: true);
        RemoveOtherVersions(folder, key, path);
        return ToImage(bytes);
    }

    /// <summary>
    /// The version of a photo's image: 16 hex digits of a SHA-256 of Immich's thumbhash and update
    /// time. It's safe in a file name and in an ETag.
    /// </summary>
    public static string VersionOf(string? thumbhash, DateTime immichUpdatedAt)
    {
        var text = $"{thumbhash}|{immichUpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>Deletes the files of older versions of the photo. A file that another request is writing is left alone.</summary>
    private static void RemoveOtherVersions(string folder, string key, string current)
    {
        foreach (var file in Directory.EnumerateFiles(folder, $"{key}*"))
        {
            if (file == current || file.EndsWith(".tmp", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Another request is reading it. The next fetch of a newer version tries again.
            }
        }
    }

    /// <summary>Immich sends WebP or JPEG. The first bytes say which.</summary>
    private static StoredImage ToImage(byte[] bytes)
    {
        var isWebp = bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        var isPng = bytes.Length >= 4 && bytes.AsSpan(0, 4).SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]);
        return new StoredImage(bytes, isWebp ? "image/webp" : isPng ? "image/png" : "image/jpeg");
    }
}
