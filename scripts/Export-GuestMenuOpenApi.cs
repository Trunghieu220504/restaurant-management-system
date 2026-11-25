#:project ../tests/Restaurant.IntegrationTests/Restaurant.IntegrationTests.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using Microsoft.AspNetCore.Hosting;
using Restaurant.IntegrationTests;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

var root = Directory.GetCurrentDirectory();
var contentRoot = Path.Combine(root, "src", "Host", "Restaurant.Api");
if (args.Length != 0 || !File.Exists(Path.Combine(contentRoot, "Restaurant.Api.csproj")))
    throw new InvalidOperationException("Run without arguments from the repository root.");
var output = Path.Combine(root, "docs", "api", "openapi.json");
var committed = JsonNode.Parse(await File.ReadAllTextAsync(output))!.AsObject();
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_RESTAURANT_API", contentRoot);
await using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
using var client = factory.CreateClient();
var actual = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!.AsObject();
foreach (var name in new[] { "PublicMenu", "ProblemDetails" })
    if (!JsonNode.DeepEquals(committed["components"]!["schemas"]![name], actual["components"]!["schemas"]![name]))
        throw new InvalidOperationException("Shared schema changed: " + name);
var menu = actual["paths"]!["/api/public/menu"]!.DeepClone();
var menuOperation = menu["get"]!;
menuOperation["description"] = "Public read-only menu. When a Guest credential is supplied, it must be valid for the active session.";
menuOperation["responses"]!["401"] ??= new JsonObject { ["description"] = "Unauthorized" };
menuOperation["responses"]!["429"] ??= new JsonObject { ["description"] = "Too Many Requests" };
committed["paths"]!["/api/public/menu"] = menu;
var session = actual["paths"]!["/api/public/guest-access/session"]!.DeepClone();
session["get"]!["description"] = "Requires the session-scoped Authorization: Guest <token> header.";
session["get"]!["responses"]!["401"] ??= new JsonObject { ["description"] = "Unauthorized" };
session["get"]!["responses"]!["429"] ??= new JsonObject { ["description"] = "Too Many Requests" };
committed["paths"]!["/api/public/guest-access/session"] = session;
foreach (var path in new[] { "/api/public/guest-access/batches", "/api/public/guest-access/batches/{id}" })
    committed["paths"]![path] = actual["paths"]![path]!.DeepClone();
foreach (var name in new[] { "GuestSession", "GuestBatchRequest", "GuestBatchReceipt", "BatchLine" })
    committed["components"]!["schemas"]![name] = actual["components"]!["schemas"]![name]!.DeepClone();
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
await File.WriteAllTextAsync(output, committed.ToJsonString(options).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
Console.WriteLine("Exported guest menu, session and ordering endpoints.");
