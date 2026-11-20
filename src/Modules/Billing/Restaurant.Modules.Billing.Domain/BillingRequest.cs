namespace Restaurant.Modules.Billing.Domain;

public sealed class BillingRequest
{
    public Guid Key { get; set; }
    public string Fingerprint { get; set; } = "";
    public long InvoiceId { get; set; }
    public long ResourceId { get; set; }
}
