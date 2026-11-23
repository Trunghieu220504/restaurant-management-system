using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Fulfillment.Infrastructure.Persistence;

namespace Restaurant.Modules.Fulfillment.Infrastructure;

public sealed record FinalTicket(long PortionId, long? AdjustmentIncidentId);

public static class TicketSettlement
{
    // Read the ticket and incident tables owned by InitialFulfillment, including on deployments
    // where Sales is already live and the Fulfillment migration has yet to roll out.
    public static async Task<List<FinalTicket>> FinalAsync(FulfillmentDbContext db, long[] portions, CancellationToken ct)
    {
        if (portions.Length == 0) return [];
        var installed = await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID(N'fulfillment.PhieuCheBien', N'U') IS NOT NULL
                AND OBJECT_ID(N'fulfillment.SuCoMon', N'U') IS NOT NULL THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct);
        if (installed == 0) return [];
        var ids = JsonSerializer.Serialize(portions);
        return await db.Database.SqlQuery<FinalTicket>($"""
            SELECT requested.Id AS PortionId, adjustment.Id AS AdjustmentIncidentId
            FROM OPENJSON({ids}) WITH (Id bigint '$') AS requested
            CROSS APPLY (
                SELECT TOP (1) p.Id, p.TrangThai
                FROM fulfillment.PhieuCheBien p WITH (HOLDLOCK)
                WHERE p.ChiTietDonId = requested.Id ORDER BY p.Lan DESC
            ) AS lastTicket
            OUTER APPLY (
                SELECT TOP (1) s.Id FROM fulfillment.SuCoMon s WITH (HOLDLOCK)
                WHERE s.PhieuCheBienId = lastTicket.Id AND s.TrangThai = 'DA_DUYET'
                    AND s.QuyetDinh = 'HUY_GIAM_NGHIA_VU' ORDER BY s.Id DESC
            ) AS adjustment
            WHERE lastTicket.TrangThai IN ('DA_GIAO', 'DA_HUY')
              AND NOT EXISTS (
                SELECT 1 FROM fulfillment.PhieuCheBien p WITH (HOLDLOCK)
                JOIN fulfillment.SuCoMon s WITH (HOLDLOCK) ON s.PhieuCheBienId = p.Id
                WHERE p.ChiTietDonId = requested.Id AND
                      (s.TrangThai = 'CHO_DUYET' OR
                       (p.Id = lastTicket.Id AND s.TrangThai = 'DA_DUYET' AND s.QuyetDinh = 'LAM_LAI'))
              )
            """).ToListAsync(ct);
    }
}
