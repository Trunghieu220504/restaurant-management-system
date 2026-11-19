using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Restaurant.Modules.Catalog.Application;

namespace Restaurant.Modules.Catalog.Presentation;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/catalog")
            .WithTags("Catalog")
            .RequireAuthorization();

        group.MapGet("/menu", async (ICatalogMenu menu, CancellationToken ct) => Results.Ok(await menu.GetMenuAsync(ct)))
            .WithName("CatalogGetMenu").Produces<MenuDto>(200).Produces(401)
            .WithDescription("Employee menu, including inactive records for management. Prices are integer VND. Sold-out flags and images are outside this milestone.");

        var manage = group.MapGroup("").RequireAuthorization(policy => policy.RequireClaim("permission", "catalog.menu.manage"));
        manage.MapPost("/groups", async (CreateMenuGroupRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.CreateGroupAsync(request, ct), x => $"/api/catalog/groups/{x.Id}"))
            .WithName("CatalogCreateGroup").Produces<MenuGroupDto>(201).ProducesProblem(400).ProducesProblem(409);
        manage.MapPut("/groups/{id:guid}", async (Guid id, UpdateMenuGroupRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.UpdateGroupAsync(id, request, ct)))
            .WithName("CatalogUpdateGroup").Produces<MenuGroupDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPost("/items", async (CreateMenuItemRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.CreateItemAsync(request, ct), x => $"/api/catalog/items/{x.Id}"))
            .WithName("CatalogCreateItem").Produces<MenuItemDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPut("/items/{id:guid}", async (Guid id, UpdateMenuItemRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.UpdateItemAsync(id, request, ct)))
            .WithName("CatalogUpdateItem").Produces<MenuItemDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPost("/items/{itemId:guid}/variants", async (Guid itemId, CreateVariantRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.CreateVariantAsync(itemId, request, ct), x => $"/api/catalog/items/{itemId}/variants/{x.Id}"))
            .WithName("CatalogCreateVariant").Produces<VariantDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPut("/items/{itemId:guid}/variants/{id:guid}", async (Guid itemId, Guid id, UpdateVariantRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.UpdateVariantAsync(itemId, id, request, ct)))
            .WithName("CatalogUpdateVariant").Produces<VariantDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPost("/option-groups", async (CreateOptionGroupRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.CreateOptionGroupAsync(request, ct), x => $"/api/catalog/option-groups/{x.Id}"))
            .WithName("CatalogCreateOptionGroup").Produces<OptionGroupDto>(201).ProducesProblem(400).ProducesProblem(409);
        manage.MapPut("/option-groups/{id:guid}", async (Guid id, UpdateOptionGroupRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.UpdateOptionGroupAsync(id, request, ct)))
            .WithName("CatalogUpdateOptionGroup").Produces<OptionGroupDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPost("/option-groups/{groupId:guid}/options", async (Guid groupId, CreateOptionRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.CreateOptionAsync(groupId, request, ct), x => $"/api/catalog/option-groups/{groupId}/options/{x.Id}"))
            .WithName("CatalogCreateOption").Produces<OptionDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPut("/option-groups/{groupId:guid}/options/{id:guid}", async (Guid groupId, Guid id, UpdateOptionRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.UpdateOptionAsync(groupId, id, request, ct)))
            .WithName("CatalogUpdateOption").Produces<OptionDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapPut("/items/{itemId:guid}/option-groups/{groupId:guid}", async (Guid itemId, Guid groupId, AssignOptionGroupRequest request, ICatalogMenu menu, CancellationToken ct) =>
                Respond(await menu.AssignOptionGroupAsync(itemId, groupId, request, ct)))
            .WithName("CatalogAssignOptionGroup").Produces<ItemOptionGroupDto>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        manage.MapDelete("/items/{itemId:guid}/option-groups/{groupId:guid}", async (Guid itemId, Guid groupId, ICatalogMenu menu, CancellationToken ct) =>
            {
                var result = await menu.RemoveOptionGroupAsync(itemId, groupId, ct);
                return result.Error is null ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("CatalogRemoveOptionGroup").Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return group;
    }

    private static IResult Respond<T>(CatalogResult<T> result, Func<T, string>? created = null) =>
        result.Error is not null ? Problem(result.Error) : created is null ? Results.Ok(result.Value) : Results.Created(created(result.Value!), result.Value);

    private static IResult Problem(string code) => Results.Problem(
        statusCode: code is CatalogErrors.InputInvalid or CatalogErrors.SelectionRangeInvalid ? 400 :
            code.EndsWith(".not_found", StringComparison.Ordinal) ? 404 : 409,
        title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
}
