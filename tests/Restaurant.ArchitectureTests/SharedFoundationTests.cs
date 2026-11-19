using System.Xml.Linq;
using Xunit;

namespace Restaurant.ArchitectureTests;

public sealed class SharedFoundationTests
{
    [Fact]
    public void Shared_contracts_are_framework_free_and_sql_helper_is_isolated()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RestaurantManagement.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var root = directory.FullName;
        var contractsPath = Path.Combine(root, "src/Shared/Restaurant.Shared.Contracts/Restaurant.Shared.Contracts.csproj");
        Assert.True(File.Exists(contractsPath), "Missing shared contracts project.");
        var contracts = XDocument.Load(contractsPath);
        Assert.Empty(contracts.Descendants("ProjectReference"));
        Assert.Empty(contracts.Descendants("PackageReference"));
        Assert.Empty(contracts.Descendants("FrameworkReference"));
        var sql = XDocument.Load(Path.Combine(root, "src/Shared/Restaurant.Shared.SqlServer/Restaurant.Shared.SqlServer.csproj"));
        Assert.Empty(sql.Descendants("ProjectReference"));
        Assert.Empty(sql.Descendants("PackageReference"));
        Assert.Empty(sql.Descendants("FrameworkReference"));
    }
}
