namespace Restaurant.Modules.Catalog.Application;

public sealed record ImageUploadRequest(int Bytes);
public sealed record ImageUpload(Guid UploadId, string UploadUrl);
public sealed record AttachImageRequest(Guid UploadId, string Version);
public interface IMenuImages
{
    Task<CatalogResult<ImageUpload>> PrepareAsync(Guid itemId, ImageUploadRequest request, CancellationToken ct);
    Task<CatalogResult<string>> AttachAsync(Guid itemId, AttachImageRequest request, CancellationToken ct);
}
