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
var path = Path.Combine(root, "docs", "api", "openapi.json");
var committed = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_RESTAURANT_API", contentRoot);
await using var factory = new ApiFactory().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
using var client = factory.CreateClient();
var actual = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!.AsObject();
var actualSchemas = actual["components"]!["schemas"]!.AsObject();
var schemas = committed["components"]!["schemas"]!.AsObject();
var visited = new HashSet<string>(StringComparer.Ordinal);
foreach (var (name, operation) in actual["paths"]!.AsObject().Where(x => x.Key.StartsWith("/api/fulfillment/", StringComparison.Ordinal)))
{
    committed["paths"]![name] = operation!.DeepClone();
    CopyReferences(operation);
}
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
await File.WriteAllTextAsync(path, committed.ToJsonString(options).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
Console.WriteLine("Exported Fulfillment HTTP metadata from TestServer; no database operation ran.");

void CopyReferences(JsonNode? node)
{
    if (node is JsonObject obj)
    {
        foreach (var (key, value) in obj)
        {
            if (key == "$ref" && value is JsonValue text && text.TryGetValue<string>(out var reference))
            {
                const string prefix = "#/components/schemas/";
                if (!reference.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected reference.");
                var name = reference[prefix.Length..];
                if (!visited.Add(name)) continue;
                var schema = actualSchemas[name] ?? throw new InvalidOperationException("Missing schema " + name);
                if (name is "TicketView" or "TicketCommand" or "OperationsSnapshot" or "TerminalTicket")
                    schemas[name] = schema.DeepClone();
                else if (!JsonNode.DeepEquals(schemas[name], schema))
                    throw new InvalidOperationException("Shared schema mismatch: " + name);
                CopyReferences(schema);
            }
            else CopyReferences(value);
        }
    }
    else if (node is JsonArray array) foreach (var value in array) CopyReferences(value);
}
