using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;

namespace Restaurant.Modules.Catalog.Infrastructure;

internal sealed class MenuImages(CatalogDbContext db, IConfiguration configuration) : IMenuImages
{
    private const int MaxBytes = 1024 * 1024;
    private const string MediaType = "image/webp";
    private string? Bucket => configuration["MenuImages:Bucket"];

    private AmazonS3Client? Client()
    {
        var section = configuration.GetSection("MenuImages");
        if (!Regex.IsMatch(section["AccountId"] ?? "", "^[0-9a-f]{32}$") ||
            string.IsNullOrWhiteSpace(Bucket) || string.IsNullOrEmpty(section["AccessKeyId"]) ||
            string.IsNullOrEmpty(section["SecretAccessKey"]) ||
            !Uri.TryCreate(section["PublicBaseUrl"], UriKind.Absolute, out var media) || media.Scheme != "https") return null;
        return new AmazonS3Client(section["AccessKeyId"], section["SecretAccessKey"], new AmazonS3Config
        {
            ServiceURL = $"https://{section["AccountId"]}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto", ForcePathStyle = true
        });
    }

    private static string Incoming(Guid item, Guid upload) => $"incoming/menu/{item:N}/{upload:N}.webp";

    public async Task<CatalogResult<ImageUpload>> PrepareAsync(Guid itemId, ImageUploadRequest request, CancellationToken ct)
    {
        if (request.Bytes is < 12 or > MaxBytes) return CatalogResult<ImageUpload>.Fail(CatalogErrors.InputInvalid);
        using var s3 = Client();
        if (s3 is null) return CatalogResult<ImageUpload>.Fail("catalog.image.unconfigured");
        if (!await db.Set<Mon>().AnyAsync(i => i.Id == itemId, ct)) return CatalogResult<ImageUpload>.Fail(CatalogErrors.ItemNotFound);
        var id = Guid.NewGuid();
        var sign = new GetPreSignedUrlRequest
        {
            BucketName = Bucket, Key = Incoming(itemId, id), Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(5), ContentType = MediaType
        };
        sign.Headers.ContentLength = request.Bytes;
        return CatalogResult<ImageUpload>.Ok(new(id, await s3.GetPreSignedURLAsync(sign)));
    }

    public async Task<CatalogResult<string>> AttachAsync(Guid itemId, AttachImageRequest request, CancellationToken ct)
    {
        if (request.UploadId == Guid.Empty || !CatalogRules.TryVersion(request.Version, out var version))
            return CatalogResult<string>.Fail(CatalogErrors.InputInvalid);
        using var s3 = Client();
        if (s3 is null) return CatalogResult<string>.Fail("catalog.image.unconfigured");
        var item = await db.Set<Mon>().SingleOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return CatalogResult<string>.Fail(CatalogErrors.ItemNotFound);
        try
        {
            // The PUT URL can be reused until it expires. Publish only the bytes we verified, under an immutable key.
            using var upload = await s3.GetObjectAsync(Bucket, Incoming(itemId, request.UploadId), ct);
            if (upload.ContentLength is < 12 or > MaxBytes || upload.Headers.ContentType != MediaType)
                return CatalogResult<string>.Fail(CatalogErrors.InputInvalid);
            var bytes = new byte[(int)upload.ContentLength];
            await upload.ResponseStream.ReadExactlyAsync(bytes, ct);
            if (!bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
                return CatalogResult<string>.Fail(CatalogErrors.InputInvalid);
            var key = $"menu/{itemId:N}/{Convert.ToHexStringLower(SHA256.HashData(bytes))}.webp";
            if (item.AnhObjectKey == key) return CatalogResult<string>.Ok(key);
            if (!item.Version.SequenceEqual(version)) return CatalogResult<string>.Fail(CatalogErrors.ConcurrencyConflict);
            using var stream = new MemoryStream(bytes, writable: false);
            await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = key, InputStream = stream,
                ContentType = MediaType, UseChunkEncoding = false, DisablePayloadSigning = true, DisableDefaultChecksumValidation = true }, ct);
            item.AnhObjectKey = key;
            await db.SaveChangesAsync(ct);
            return CatalogResult<string>.Ok(key);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return CatalogResult<string>.Fail("catalog.image.not_found");
        }
        catch (DbUpdateConcurrencyException) { return CatalogResult<string>.Fail(CatalogErrors.ConcurrencyConflict); }
    }
}
