namespace Restaurant.Modules.Inventory.Domain;

public sealed record StockCost(decimal Average, decimal Usage, decimal ClosingValue, decimal Cost)
{
    public static StockCost? Calculate(decimal quantity, decimal value, decimal counted)
    {
        if (counted < 0 || quantity < counted || value < 0) return null;
        if (quantity == 0) return value == 0 ? new(0, 0, 0, 0) : null;
        var average = value / quantity;
        var end = counted == quantity ? value : counted * average;
        return new(average, quantity - counted, end, value - end);
    }
}

public sealed class DieuChinhTon
{
    public long Id { get; set; }
    public Guid NgayBanId { get; set; }
    public Guid? NgayBanGocId { get; set; }
    public long NguyenLieuId { get; set; }
    public decimal SoLuongChuan { get; set; }
    public decimal GiaTriDieuChinh { get; set; }
    public string LyDo { get; set; } = "";
    public Guid NguoiDuyetId { get; set; }
    public DateTimeOffset TaoLuc { get; set; }
    public Guid KhoaYeuCau { get; set; }
}
