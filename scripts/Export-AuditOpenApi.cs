#:project ../tests/Restaurant.IntegrationTests/Restaurant.IntegrationTests.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Restaurant.IntegrationTests;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

// Export actual Audit metadata while preserving other owners' curated HTTP/header descriptions.
// TestServer generates metadata only: no listener, migration or account operation.
var root = Directory.GetCurrentDirectory();
var contentRoot = Path.Combine(root, "src", "Host", "Restaurant.Api");
if (args.Length != 0 || !File.Exists(Path.Combine(contentRoot, "Restaurant.Api.csproj")))
    throw new InvalidOperationException("Run without arguments from the repository root.");
var output = Path.Combine(root, "docs", "api", "openapi.json");
var committed = JsonNode.Parse(await File.ReadAllTextAsync(output))!.AsObject();
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_RESTAURANT_API", contentRoot);
await using var factory = new ApiFactory().WithWebHostBuilder(builder =>
{
    builder.UseEnvironment("Development");
    builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Logging:LogLevel:Default"] = "Warning"
    }));
});
using var client = factory.CreateClient();
using var response = await client.GetAsync("/openapi/v1.json");
response.EnsureSuccessStatusCode();
var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
var operation = actual["paths"]!["/api/audit/logs"]?.AsObject()
    ?? throw new InvalidOperationException("Audit endpoint is missing from actual OpenAPI.");
if (operation.Count != 1 || operation["get"] is null)
    throw new InvalidOperationException("Audit must expose only GET.");
var currentSchemas = committed["components"]!["schemas"]!.AsObject();
var actualSchemas = actual["components"]!["schemas"]!.AsObject();
var pending = new Queue<string>(References(operation));
var visited = new HashSet<string>(StringComparer.Ordinal);
while (pending.TryDequeue(out var name))
{
    if (!visited.Add(name)) continue;
    var schema = actualSchemas[name] ?? throw new InvalidOperationException("Missing schema " + name);
    if (name.StartsWith("Audit", StringComparison.Ordinal)) currentSchemas[name] = schema.DeepClone();
    else if (!JsonNode.DeepEquals(currentSchemas[name], schema))
        throw new InvalidOperationException("Shared schema changed; coordinate its owner before exporting: " + name);
    foreach (var reference in References(schema)) pending.Enqueue(reference);
}
committed["paths"]!["/api/audit/logs"] = operation.DeepClone();
if (actual["tags"] is JsonArray tags && tags.FirstOrDefault(x => x?["name"]?.GetValue<string>() == "Audit") is { } tag)
{
    if (committed["tags"] is not JsonArray) committed["tags"] = new JsonArray();
    var currentTags = committed["tags"]!.AsArray();
    if (!currentTags.Any(x => x?["name"]?.GetValue<string>() == "Audit")) currentTags.Add(tag.DeepClone());
}
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
await File.WriteAllTextAsync(output, committed.ToJsonString(options).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
Console.WriteLine("Exported actual Audit metadata; unrelated contracts preserved. No database operation ran.");

static IEnumerable<string> References(JsonNode? node)
{
    if (node is JsonObject obj)
    {
        foreach (var (key, value) in obj)
        {
            if (key == "$ref" && value is JsonValue text && text.TryGetValue<string>(out var reference))
            {
                const string prefix = "#/components/schemas/";
                if (!reference.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected schema reference.");
                yield return reference[prefix.Length..];
            }
            else foreach (var nested in References(value)) yield return nested;
        }
    }
    else if (node is JsonArray array)
        foreach (var value in array)
            foreach (var nested in References(value)) yield return nested;
}
