using System.Xml.Linq;
using Xunit;

namespace Restaurant.ArchitectureTests;

public sealed class DependencyTests
{
    private static readonly string[] Modules =
    [
        "Identity", "Seating", "Catalog", "Sales", "Fulfillment",
        "Billing", "Inventory", "SalesDay", "Audit", "Reporting"
    ];

    // Consumer module (Application layer) -> modules whose Contracts it may reference (HIE-2.3).
    // Adding a pair here is an architecture decision: update docs/backend-foundation.md in the same PR.
    private static readonly Dictionary<string, string[]> CrossModuleContracts = new()
    {
        ["Sales"] = ["Catalog", "Seating", "SalesDay", "Billing"],
        ["Billing"] = ["Sales"]
    };

    private static readonly string[] ModulesWithContracts = ["Catalog", "Seating", "SalesDay", "Billing", "Sales", "Audit"];

    [Fact]
    public void Cross_module_pairs_only_target_modules_that_publish_contracts()
    {
        var root = FindRoot();
        foreach (var provider in CrossModuleContracts.Values.SelectMany(x => x).Distinct())
        {
            Assert.Contains(provider, ModulesWithContracts);
        }
        foreach (var module in ModulesWithContracts)
        {
            Assert.True(File.Exists(Path.Combine(root, "src", "Modules", module,
                $"Restaurant.Modules.{module}.Contracts", $"Restaurant.Modules.{module}.Contracts.csproj")),
                $"Missing Contracts project for {module}.");
        }
    }

    [Fact]
    public void Contracts_stay_pure_and_do_not_expose_persistence_or_web_types()
    {
        var root = FindRoot();
        string[] forbidden =
        [
            "Microsoft.", "System.Data", "System.Net.Http", "IQueryable", "DbConnection", "DbTransaction",
            "DbContext", "HttpContext", "IActionResult"
        ];
        foreach (var module in ModulesWithContracts)
        {
            var directory = Path.Combine(root, "src", "Modules", module, $"Restaurant.Modules.{module}.Contracts");
            var sources = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToArray();
            Assert.NotEmpty(sources);
            foreach (var source in sources)
            {
                var text = File.ReadAllText(source);
                foreach (var word in forbidden)
                {
                    Assert.False(text.Contains(word, StringComparison.Ordinal), $"{source} contains '{word}'.");
                }
            }
        }
    }

    [Fact]
    public void Every_module_has_four_isolated_projects()
    {
        var root = FindRoot();
        foreach (var module in Modules)
        {
            var contractsPath = Path.Combine(root, "src", "Modules", module,
                $"Restaurant.Modules.{module}.Contracts", $"Restaurant.Modules.{module}.Contracts.csproj");
            if (File.Exists(contractsPath))
            {
                var contracts = XDocument.Load(contractsPath);
                Assert.Empty(contracts.Descendants("ProjectReference"));
                Assert.Empty(contracts.Descendants("PackageReference"));
                Assert.Empty(contracts.Descendants("FrameworkReference"));
            }
            foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Presentation" })
            {
                var path = Path.Combine(root, "src", "Modules", module,
                    $"Restaurant.Modules.{module}.{layer}", $"Restaurant.Modules.{module}.{layer}.csproj");
                Assert.True(File.Exists(path), $"Missing project: {path}");
                var project = XDocument.Load(path);
                var references = project.Descendants("ProjectReference")
                    .Select(x => Path.GetFileNameWithoutExtension(x.Attribute("Include")!.Value.Replace('\\', '/')))
                    .ToArray();
                var required = layer switch
                {
                    "Domain" => Array.Empty<string>(),
                    "Application" => new[] { $"Restaurant.Modules.{module}.Domain" },
                    "Infrastructure" => new[] { $"Restaurant.Modules.{module}.Domain", $"Restaurant.Modules.{module}.Application" },
                    "Presentation" => new[] { $"Restaurant.Modules.{module}.Application" },
                    _ => throw new InvalidOperationException()
                };
                // Optional: the module's own Contracts (to implement them) and, for the Application
                // layer only, the Contracts of the modules listed in CrossModuleContracts.
                var optional = new List<string>();
                if (layer is "Application" or "Infrastructure")
                {
                    optional.Add($"Restaurant.Modules.{module}.Contracts");
                }
                // HIE-3.0: Infrastructure implements the shared Outbox writer, so it may reference the
                // framework-free Shared contracts (never Shared.SqlServer, which only the Host and tests use).
                if (layer == "Infrastructure")
                {
                    optional.Add("Restaurant.Shared.Contracts");
                }
                if (layer == "Application" && CrossModuleContracts.TryGetValue(module, out var providers))
                {
                    optional.AddRange(providers.Select(p => $"Restaurant.Modules.{p}.Contracts"));
                }
                Assert.Empty(required.Except(references));
                Assert.Empty(references.Except(required.Concat(optional)));
                Assert.Equal(references.Length, references.Distinct().Count());
                // Names alone are insufficient: a same-named project outside the expected module must not pass.
                foreach (var reference in project.Descendants("ProjectReference"))
                {
                    var actual = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,
                        reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)));
                    var name = Path.GetFileNameWithoutExtension(actual);
                    var expected = name == "Restaurant.Shared.Contracts"
                        ? Path.Combine(root, "src", "Shared", name, Path.GetFileName(actual))
                        : Path.Combine(root, "src", "Modules",
                            name.EndsWith(".Contracts", StringComparison.Ordinal)
                                ? name["Restaurant.Modules.".Length..^".Contracts".Length]
                                : module,
                            name, Path.GetFileName(actual));
                    Assert.Equal(Path.GetFullPath(expected), actual);
                }
                if (layer is "Domain" or "Application")
                {
                    Assert.Empty(project.Descendants("PackageReference"));
                    Assert.Empty(project.Descendants("FrameworkReference"));
                }
            }
        }
    }

    [Fact]
    public void No_module_uses_shared_build_files_to_bypass_reference_rules()
    {
        var root = FindRoot();
        var moduleRoot = Path.Combine(root, "src", "Modules");
        Assert.True(Directory.Exists(moduleRoot), "Module structure has not been created.");
        Assert.DoesNotContain(Directory.EnumerateFiles(moduleRoot, "*.props", SearchOption.AllDirectories),
            p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        Assert.Empty(XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("ProjectReference"));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
