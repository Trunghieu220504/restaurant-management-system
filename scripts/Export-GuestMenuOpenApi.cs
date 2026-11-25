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
committed["paths"]!["/api/public/menu"] = actual["paths"]!["/api/public/menu"]!.DeepClone();
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
await File.WriteAllTextAsync(output, committed.ToJsonString(options).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
Console.WriteLine("Exported actual guest menu endpoint; unrelated contracts preserved.");
