using Restaurant.Modules.Identity.Infrastructure.Initialization;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class AdminBootstrapInputTests
{
    [Theory]
    [InlineData(null, "admin@example.invalid", "not-a-real-credential")]
    [InlineData("admin", null, "not-a-real-credential")]
    [InlineData("admin", "invalid-email", "not-a-real-credential")]
    [InlineData("admin", "admin@example.invalid", null)]
    [InlineData(" admin ", "admin@example.invalid", "not-a-real-credential")]
    public void Missing_or_malformed_input_is_rejected_without_echoing_secrets(string? user, string? email, string? password)
    {
        var error = Assert.Throws<ArgumentException>(() => new AdminBootstrapInput(user, email, password));
        Assert.DoesNotContain("not-a-real-credential", error.Message);
    }

    [Fact]
    public void Valid_shape_does_not_trim_or_expose_password()
    {
        var input = new AdminBootstrapInput("admin", "admin@example.invalid", " space-is-significant ");
        Assert.Equal(" space-is-significant ", input.Password);
        Assert.DoesNotContain(input.Password, input.ToString());
    }
}
