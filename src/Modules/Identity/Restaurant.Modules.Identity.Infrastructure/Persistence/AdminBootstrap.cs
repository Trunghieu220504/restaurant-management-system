namespace Restaurant.Modules.Identity.Infrastructure.Persistence;

public sealed class AdminBootstrap
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
