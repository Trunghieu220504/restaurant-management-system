using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Modules.Catalog.Application;
using Restaurant.Modules.Catalog.Domain;
using Restaurant.Modules.Catalog.Infrastructure.Persistence;

namespace Restaurant.Modules.Catalog.Infrastructure;

/// <summary>Restaurant-wide catalog. Every write validates references and selection limits in one transaction.</summary>
public sealed class CatalogMenu(CatalogDbContext db) : ICatalogMenu
{
    public async Task<MenuDto> GetMenuAsync(CancellationToken ct)
    {
        // Return inactive records as well: management needs them to reactivate or correct a menu.
        var groups = await db.Set<NhomMon>().AsNoTracking().OrderBy(x => x.ThuTu).ThenBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct);
        var items = await db.Set<Mon>().AsNoTracking().OrderBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct);
        var variants = await db.Set<BienThe>().AsNoTracking().OrderBy(x => x.Gia).ThenBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct);
        var optionGroups = await db.Set<NhomTuyChon>().AsNoTracking().OrderBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct);
        var options = await db.Set<TuyChon>().AsNoTracking().OrderBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct);
        var assignments = await db.Set<MonNhomTuyChon>().AsNoTracking().OrderBy(x => x.NhomTuyChonId).ToListAsync(ct);
        var variantsByItem = variants.ToLookup(x => x.MonId);
        var assignmentsByItem = assignments.ToLookup(x => x.MonId);
        var optionsByGroup = options.ToLookup(x => x.NhomTuyChonId);
        return new MenuDto(groups.Select(Map).ToArray(),
            items.Select(x => Map(x, variantsByItem[x.Id], assignmentsByItem[x.Id])).ToArray(),
            optionGroups.Select(x => Map(x, optionsByGroup[x.Id])).ToArray());
    }

    public Task<CatalogResult<MenuGroupDto>> CreateGroupAsync(CreateMenuGroupRequest request, CancellationToken ct) =>
        WriteAsync<MenuGroupDto>(async () =>
        {
            if (request is null || !CatalogRules.ValidName(request.Name) || request.SortOrder < 0)
                return Fail<MenuGroupDto>(CatalogErrors.InputInvalid);
            var row = new NhomMon { Id = Guid.NewGuid(), Ten = request.Name.Trim(), ThuTu = request.SortOrder };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return CatalogResult<MenuGroupDto>.Ok(Map(row));
        }, ct);

    public Task<CatalogResult<MenuGroupDto>> UpdateGroupAsync(Guid id, UpdateMenuGroupRequest request, CancellationToken ct) =>
        WriteAsync<MenuGroupDto>(async () =>
        {
            if (id == Guid.Empty || request is null || !CatalogRules.ValidName(request.Name) || request.SortOrder < 0 ||
                !CatalogRules.TryVersion(request.Version, out var version)) return Fail<MenuGroupDto>(CatalogErrors.InputInvalid);
            var row = await db.Set<NhomMon>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Fail<MenuGroupDto>(CatalogErrors.GroupNotFound);
            if (!row.Version.SequenceEqual(version)) return Fail<MenuGroupDto>(CatalogErrors.ConcurrencyConflict);
            row.Ten = request.Name.Trim();
            row.ThuTu = request.SortOrder;
            row.HoatDong = request.Active;
            await db.SaveChangesAsync(ct);
            return CatalogResult<MenuGroupDto>.Ok(Map(row));
        }, ct);

    public Task<CatalogResult<MenuItemDto>> CreateItemAsync(CreateMenuItemRequest request, CancellationToken ct) =>
        WriteAsync<MenuItemDto>(async () =>
        {
            if (request is null || !ValidItem(request.GroupId, request.Code, request.Name, request.Station))
                return Fail<MenuItemDto>(CatalogErrors.InputInvalid);
            var parent = await db.Set<NhomMon>().SingleOrDefaultAsync(x => x.Id == request.GroupId, ct);
            if (parent is null) return Fail<MenuItemDto>(CatalogErrors.GroupNotFound);
            if (!parent.HoatDong) return Fail<MenuItemDto>(CatalogErrors.GroupInactive);
            var code = request.Code.Trim();
            if (await db.Set<Mon>().AnyAsync(x => x.Ma == code, ct)) return Fail<MenuItemDto>(CatalogErrors.ItemCodeConflict);
            var row = new Mon { Id = Guid.NewGuid(), NhomMonId = parent.Id, Ma = code, Ten = request.Name.Trim(), Tram = request.Station };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return CatalogResult<MenuItemDto>.Ok(Map(row, [], []));
        }, ct, CatalogErrors.ItemCodeConflict);

    public Task<CatalogResult<MenuItemDto>> UpdateItemAsync(Guid id, UpdateMenuItemRequest request, CancellationToken ct) =>
        WriteAsync<MenuItemDto>(async () =>
        {
            if (id == Guid.Empty || request is null || !ValidItem(request.GroupId, request.Code, request.Name, request.Station) ||
                !CatalogRules.TryVersion(request.Version, out var version)) return Fail<MenuItemDto>(CatalogErrors.InputInvalid);
            var row = await db.Set<Mon>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Fail<MenuItemDto>(CatalogErrors.ItemNotFound);
            if (!row.Version.SequenceEqual(version)) return Fail<MenuItemDto>(CatalogErrors.ConcurrencyConflict);
            var parent = await db.Set<NhomMon>().SingleOrDefaultAsync(x => x.Id == request.GroupId, ct);
            if (parent is null) return Fail<MenuItemDto>(CatalogErrors.GroupNotFound);
            if (request.Active && !parent.HoatDong) return Fail<MenuItemDto>(CatalogErrors.GroupInactive);
            var code = request.Code.Trim();
            if (await db.Set<Mon>().AnyAsync(x => x.Id != id && x.Ma == code, ct)) return Fail<MenuItemDto>(CatalogErrors.ItemCodeConflict);
            row.NhomMonId = parent.Id;
            row.Ma = code;
            row.Ten = request.Name.Trim();
            row.Tram = request.Station;
            row.HoatDong = request.Active;
            await db.SaveChangesAsync(ct);
            return CatalogResult<MenuItemDto>.Ok(await ItemAsync(row, ct));
        }, ct, CatalogErrors.ItemCodeConflict);

    public Task<CatalogResult<VariantDto>> CreateVariantAsync(Guid itemId, CreateVariantRequest request, CancellationToken ct) =>
        WriteAsync<VariantDto>(async () =>
        {
            if (itemId == Guid.Empty || request is null || !ValidVariant(request.Code, request.Name, request.Price))
                return Fail<VariantDto>(CatalogErrors.InputInvalid);
            var item = await db.Set<Mon>().SingleOrDefaultAsync(x => x.Id == itemId, ct);
            if (item is null) return Fail<VariantDto>(CatalogErrors.ItemNotFound);
            if (!item.HoatDong) return Fail<VariantDto>(CatalogErrors.ItemInactive);
            var code = request.Code.Trim();
            if (await db.Set<BienThe>().AnyAsync(x => x.MonId == itemId && x.Ma == code, ct))
                return Fail<VariantDto>(CatalogErrors.VariantCodeConflict);
            var row = new BienThe { Id = Guid.NewGuid(), MonId = itemId, Ma = code, Ten = request.Name.Trim(), Gia = request.Price };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return CatalogResult<VariantDto>.Ok(Map(row));
        }, ct, CatalogErrors.VariantCodeConflict);

    public Task<CatalogResult<VariantDto>> UpdateVariantAsync(Guid itemId, Guid id, UpdateVariantRequest request, CancellationToken ct) =>
        WriteAsync<VariantDto>(async () =>
        {
            if (itemId == Guid.Empty || id == Guid.Empty || request is null || !ValidVariant(request.Code, request.Name, request.Price) ||
                !CatalogRules.TryVersion(request.Version, out var version)) return Fail<VariantDto>(CatalogErrors.InputInvalid);
            // Parent ownership is part of the lookup; a variant from another dish can never be modified.
            var row = await db.Set<BienThe>().SingleOrDefaultAsync(x => x.Id == id && x.MonId == itemId, ct);
            if (row is null) return Fail<VariantDto>(CatalogErrors.VariantNotFound);
            if (!row.Version.SequenceEqual(version)) return Fail<VariantDto>(CatalogErrors.ConcurrencyConflict);
            var code = request.Code.Trim();
            if (await db.Set<BienThe>().AnyAsync(x => x.Id != id && x.MonId == itemId && x.Ma == code, ct))
                return Fail<VariantDto>(CatalogErrors.VariantCodeConflict);
            row.Ma = code;
            row.Ten = request.Name.Trim();
            row.Gia = request.Price;
            row.HoatDong = request.Active;
            await db.SaveChangesAsync(ct);
            return CatalogResult<VariantDto>.Ok(Map(row));
        }, ct, CatalogErrors.VariantCodeConflict);

    public Task<CatalogResult<OptionGroupDto>> CreateOptionGroupAsync(CreateOptionGroupRequest request, CancellationToken ct) =>
        WriteAsync<OptionGroupDto>(async () =>
        {
            if (request is null || !CatalogRules.ValidName(request.Name) || !CatalogRules.ValidOptionType(request.Type))
                return Fail<OptionGroupDto>(CatalogErrors.InputInvalid);
            var row = new NhomTuyChon { Id = Guid.NewGuid(), Ten = request.Name.Trim(), Loai = request.Type };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return CatalogResult<OptionGroupDto>.Ok(Map(row, []));
        }, ct);

    public Task<CatalogResult<OptionGroupDto>> UpdateOptionGroupAsync(Guid id, UpdateOptionGroupRequest request, CancellationToken ct) =>
        WriteAsync<OptionGroupDto>(async () =>
        {
            if (id == Guid.Empty || request is null || !CatalogRules.ValidName(request.Name) || !CatalogRules.ValidOptionType(request.Type) ||
                !CatalogRules.TryVersion(request.Version, out var version)) return Fail<OptionGroupDto>(CatalogErrors.InputInvalid);
            var row = await db.Set<NhomTuyChon>().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Fail<OptionGroupDto>(CatalogErrors.OptionGroupNotFound);
            if (!row.Version.SequenceEqual(version)) return Fail<OptionGroupDto>(CatalogErrors.ConcurrencyConflict);
            row.Ten = request.Name.Trim();
            row.Loai = request.Type;
            row.HoatDong = request.Active;
            await db.SaveChangesAsync(ct);
            var options = await db.Set<TuyChon>().AsNoTracking().Where(x => x.NhomTuyChonId == id).OrderBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct);
            return CatalogResult<OptionGroupDto>.Ok(Map(row, options));
        }, ct);

    public Task<CatalogResult<OptionDto>> CreateOptionAsync(Guid groupId, CreateOptionRequest request, CancellationToken ct) =>
        WriteAsync<OptionDto>(async () =>
        {
            if (groupId == Guid.Empty || request is null || !CatalogRules.ValidName(request.Name) || !CatalogRules.ValidPrice(request.ExtraPrice))
                return Fail<OptionDto>(CatalogErrors.InputInvalid);
            var group = await db.Set<NhomTuyChon>().SingleOrDefaultAsync(x => x.Id == groupId, ct);
            if (group is null) return Fail<OptionDto>(CatalogErrors.OptionGroupNotFound);
            if (!group.HoatDong) return Fail<OptionDto>(CatalogErrors.OptionGroupInactive);
            var row = new TuyChon { Id = Guid.NewGuid(), NhomTuyChonId = groupId, Ten = request.Name.Trim(), GiaCong = request.ExtraPrice };
            db.Add(row);
            await db.SaveChangesAsync(ct);
            return CatalogResult<OptionDto>.Ok(Map(row));
        }, ct);

    public Task<CatalogResult<OptionDto>> UpdateOptionAsync(Guid groupId, Guid id, UpdateOptionRequest request, CancellationToken ct) =>
        WriteAsync<OptionDto>(async () =>
        {
            if (groupId == Guid.Empty || id == Guid.Empty || request is null || !CatalogRules.ValidName(request.Name) || !CatalogRules.ValidPrice(request.ExtraPrice) ||
                !CatalogRules.TryVersion(request.Version, out var version)) return Fail<OptionDto>(CatalogErrors.InputInvalid);
            var row = await db.Set<TuyChon>().SingleOrDefaultAsync(x => x.Id == id && x.NhomTuyChonId == groupId, ct);
            if (row is null) return Fail<OptionDto>(CatalogErrors.OptionNotFound);
            if (!row.Version.SequenceEqual(version)) return Fail<OptionDto>(CatalogErrors.ConcurrencyConflict);
            if (row.HoatDong && !request.Active)
            {
                var remaining = await db.Set<TuyChon>().CountAsync(x => x.NhomTuyChonId == groupId && x.Id != id && x.HoatDong, ct);
                if (await db.Set<MonNhomTuyChon>().AnyAsync(x => x.NhomTuyChonId == groupId && x.ChonToiDa > remaining, ct))
                    return Fail<OptionDto>(CatalogErrors.OptionRequired);
            }
            row.Ten = request.Name.Trim();
            row.GiaCong = request.ExtraPrice;
            row.HoatDong = request.Active;
            await db.SaveChangesAsync(ct);
            return CatalogResult<OptionDto>.Ok(Map(row));
        }, ct);

    public Task<CatalogResult<ItemOptionGroupDto>> AssignOptionGroupAsync(Guid itemId, Guid groupId, AssignOptionGroupRequest request, CancellationToken ct) =>
        WriteAsync<ItemOptionGroupDto>(async () =>
        {
            if (itemId == Guid.Empty || groupId == Guid.Empty || request is null) return Fail<ItemOptionGroupDto>(CatalogErrors.InputInvalid);
            var item = await db.Set<Mon>().SingleOrDefaultAsync(x => x.Id == itemId, ct);
            if (item is null) return Fail<ItemOptionGroupDto>(CatalogErrors.ItemNotFound);
            if (!item.HoatDong) return Fail<ItemOptionGroupDto>(CatalogErrors.ItemInactive);
            var group = await db.Set<NhomTuyChon>().SingleOrDefaultAsync(x => x.Id == groupId, ct);
            if (group is null) return Fail<ItemOptionGroupDto>(CatalogErrors.OptionGroupNotFound);
            if (!group.HoatDong) return Fail<ItemOptionGroupDto>(CatalogErrors.OptionGroupInactive);
            var activeCount = await db.Set<TuyChon>().CountAsync(x => x.NhomTuyChonId == groupId && x.HoatDong, ct);
            if (!CatalogRules.ValidSelections(request.MinSelections, request.MaxSelections, activeCount))
                return Fail<ItemOptionGroupDto>(CatalogErrors.SelectionRangeInvalid);
            var row = await db.Set<MonNhomTuyChon>().SingleOrDefaultAsync(x => x.MonId == itemId && x.NhomTuyChonId == groupId, ct);
            if (row is null)
            {
                row = new MonNhomTuyChon { MonId = itemId, NhomTuyChonId = groupId };
                db.Add(row);
            }
            row.ChonToiThieu = request.MinSelections;
            row.ChonToiDa = request.MaxSelections;
            await db.SaveChangesAsync(ct);
            return CatalogResult<ItemOptionGroupDto>.Ok(Map(row));
        }, ct);

    public Task<CatalogResult<bool>> RemoveOptionGroupAsync(Guid itemId, Guid groupId, CancellationToken ct) =>
        WriteAsync<bool>(async () =>
        {
            if (itemId == Guid.Empty || groupId == Guid.Empty) return Fail<bool>(CatalogErrors.InputInvalid);
            if (!await db.Set<Mon>().AnyAsync(x => x.Id == itemId, ct)) return Fail<bool>(CatalogErrors.ItemNotFound);
            if (!await db.Set<NhomTuyChon>().AnyAsync(x => x.Id == groupId, ct)) return Fail<bool>(CatalogErrors.OptionGroupNotFound);
            var row = await db.Set<MonNhomTuyChon>().SingleOrDefaultAsync(x => x.MonId == itemId && x.NhomTuyChonId == groupId, ct);
            if (row is not null)
            {
                db.Remove(row);
                await db.SaveChangesAsync(ct);
            }
            return CatalogResult<bool>.Ok(true);
        }, ct);

    private async Task<MenuItemDto> ItemAsync(Mon row, CancellationToken ct) => Map(row,
        await db.Set<BienThe>().AsNoTracking().Where(x => x.MonId == row.Id).OrderBy(x => x.Gia).ThenBy(x => x.Ten).ThenBy(x => x.Id).ToListAsync(ct),
        await db.Set<MonNhomTuyChon>().AsNoTracking().Where(x => x.MonId == row.Id).OrderBy(x => x.NhomTuyChonId).ToListAsync(ct));

    private static bool ValidItem(Guid groupId, string code, string name, string station) =>
        groupId != Guid.Empty && CatalogRules.ValidCode(code) && CatalogRules.ValidName(name) && CatalogRules.ValidStation(station);
    private static bool ValidVariant(string code, string name, long price) =>
        CatalogRules.ValidCode(code) && CatalogRules.ValidName(name) && CatalogRules.ValidPrice(price);
    private static CatalogResult<T> Fail<T>(string error) => CatalogResult<T>.Fail(error);
    private static MenuGroupDto Map(NhomMon row) => new(row.Id, row.Ten, row.ThuTu, row.HoatDong, Convert.ToBase64String(row.Version));
    private static VariantDto Map(BienThe row) => new(row.Id, row.MonId, row.Ma, row.Ten, row.Gia, row.HoatDong, Convert.ToBase64String(row.Version));
    private static OptionDto Map(TuyChon row) => new(row.Id, row.NhomTuyChonId, row.Ten, row.GiaCong, row.HoatDong, Convert.ToBase64String(row.Version));
    private static ItemOptionGroupDto Map(MonNhomTuyChon row) => new(row.NhomTuyChonId, row.ChonToiThieu, row.ChonToiDa);
    private static OptionGroupDto Map(NhomTuyChon row, IEnumerable<TuyChon> options) =>
        new(row.Id, row.Ten, row.Loai, row.HoatDong, Convert.ToBase64String(row.Version), options.Select(Map).ToArray());
    private static MenuItemDto Map(Mon row, IEnumerable<BienThe> variants, IEnumerable<MonNhomTuyChon> assignments) =>
        new(row.Id, row.NhomMonId, row.Ma, row.Ten, row.Tram, row.AnhObjectKey, row.HoatDong,
            Convert.ToBase64String(row.Version), variants.Select(Map).ToArray(), assignments.Select(Map).ToArray());

    private async Task<CatalogResult<T>> WriteAsync<T>(Func<Task<CatalogResult<T>>> action, CancellationToken ct,
        string conflict = CatalogErrors.ConcurrencyConflict)
    {
        try
        {
            // Holds reference/range locks until commit, including option counts and assignment constraints.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            if (result.Error is null) await tx.CommitAsync(ct);
            else db.ChangeTracker.Clear();
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Fail<T>(CatalogErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627 or 1205)
        {
            db.ChangeTracker.Clear();
            return Fail<T>(sql.Number == 1205 ? CatalogErrors.ConcurrencyConflict : conflict);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            db.ChangeTracker.Clear();
            return Fail<T>(CatalogErrors.ConcurrencyConflict);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is
            SqlException { Number: 1205 } or DbUpdateException { InnerException: SqlException { Number: 1205 } })
        {
            // EF's non-retrying SQL strategy wraps transient errors, including deadlock victims.
            db.ChangeTracker.Clear();
            return Fail<T>(CatalogErrors.ConcurrencyConflict);
        }
    }
}
