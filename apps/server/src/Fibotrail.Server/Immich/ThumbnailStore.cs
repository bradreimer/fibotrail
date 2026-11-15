using Fibotrail.Immich;
using Fibotrail.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Fibotrail.Server.Immich;

/// <summary>An image, ready to send to the browser.</summary>
public sealed record StoredImage(byte[] Bytes, string ContentType);

/// <summary>
/// Fetches images from Immich for the browser, so the browser never needs the Immich key. Fetched
/// images are kept in the data folder. The cache can be deleted at any time and refills on demand.
/// </summary>
public sealed class ThumbnailStore(FibotrailDbContext db, IImmichClient immich, ServerSettings settings)
{
    /// <returns>The image, or null when the ID isn't a photo in Fibotrail's database.</returns>
    public async Task<StoredImage?> GetAsync(string id, ImmichImageSize size, CancellationToken cancellationToken)
    {
        // Only a well-formed ID that the scanner stored is served. This also keeps the file path safe.
        if (!Guid.TryParseExact(id, "D", out var assetId))
        {
            return null;
        }

        var key = assetId.ToString();
        if (!await db.Photos.AnyAsync(photo => photo.Id == key, cancellationToken))
        {
            return null;
        }

        var folder = Path.Combine(settings.ThumbnailDirectory, size == ImmichImageSize.Preview ? "preview" : "thumbnail", key[..2]);
        var path = Path.Combine(folder, key);
        if (File.Exists(path))
        {
            return ToImage(await File.ReadAllBytesAsync(path, cancellationToken));
        }

        byte[] bytes;
        try
        {
            bytes = await immich.GetImageAsync(assetId, size, cancellationToken);
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
        return ToImage(bytes);
    }

    /// <summary>Immich sends WebP or JPEG. The first bytes say which.</summary>
    private static StoredImage ToImage(byte[] bytes)
    {
        var isWebp = bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        var isPng = bytes.Length >= 4 && bytes.AsSpan(0, 4).SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]);
        return new StoredImage(bytes, isWebp ? "image/webp" : isPng ? "image/png" : "image/jpeg");
    }
}
