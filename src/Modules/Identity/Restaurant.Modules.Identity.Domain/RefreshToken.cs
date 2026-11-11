namespace Restaurant.Modules.Identity.Domain;

/// <summary>
/// One link in a rotation chain. The chain is the owning <see cref="LoginSession"/>: reuse of an already-used
/// token revokes the whole session, which invalidates every token and access token that carries its id.
/// Only a hash of the secret is stored.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public DateTime? RevokedAt { get; set; }
}
