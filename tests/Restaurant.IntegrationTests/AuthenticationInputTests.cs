using Restaurant.Modules.Identity.Application.Authentication;
using Xunit;

namespace Restaurant.IntegrationTests;

public sealed class AuthenticationInputTests
{
    [Theory]
    [InlineData(null, "password")]
    [InlineData("", "password")]
    [InlineData(" admin ", "password")]
    [InlineData("admin", null)]
    [InlineData("admin", "   ")]
    public void Login_validates_shape_without_trimming_password(string? user, string? password) =>
        Assert.False(AuthenticationInput.IsValid(new LoginRequest(user, password)));

    [Fact]
    public void Bounded_input_and_password_spaces_are_preserved()
    {
        Assert.True(AuthenticationInput.IsValid(new LoginRequest("admin", " password ")));
        Assert.False(AuthenticationInput.IsValid(new LoginRequest(new string('a', 257), "password")));
        Assert.False(AuthenticationInput.IsValid(new LoginRequest("admin", new string('a', 1025))));
        Assert.False(AuthenticationInput.IsValid(new ChangePasswordRequest("password", null)));
        Assert.DoesNotContain("sensitive", new LoginRequest("admin", "sensitive").ToString());
        Assert.DoesNotContain("sensitive", new ChangePasswordRequest("sensitive", "sensitive").ToString());
    }
}
